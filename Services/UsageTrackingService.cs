using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Xaml;
using RubyDevice.Core;
using RubyDevice.Models;

namespace RubyDevice.Services;

/// <summary>
/// Service for tracking device usage time (enabled and active duration)
/// </summary>
public class UsageTrackingService : IDisposable
{
    private static UsageTrackingService? _instance;
    public static UsageTrackingService Instance => _instance ??= new UsageTrackingService();

    // Constants for timer intervals
    private const int ENABLED_TIME_UPDATE_INTERVAL_SECONDS = 60;
    private const int DATA_SAVE_INTERVAL_MINUTES = 5;

    private readonly string _dataPath;
    private readonly string _settingsPath;
    private readonly Dictionary<string, Dictionary<string, DeviceUsageRecord>> _records = new();
    private readonly TrackingSettingsFile _settings = new();
    private readonly object _lock = new();

    private Timer? _enabledTimeTimer;
    private Timer? _saveTimer;
    private DeviceManager? _deviceManager;
    private bool _disposed;

    // Handle to device ID mapping (populated from DeviceManager)
    private readonly Dictionary<IntPtr, string> _handleToDeviceId = new();

    // Track last active time for each device (for accurate ActiveSeconds calculation)
    private readonly Dictionary<string, DateTime> _lastActiveTime = new();

    // Active time accumulation threshold (milliseconds) - minimum interval to count
    private const int ACTIVE_TIME_THRESHOLD_MS = 100;

    public TrackingConfig Config => _settings.Config;
    public event EventHandler? TrackingChanged;

    private UsageTrackingService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RubyDevice");
        _dataPath = Path.Combine(appDataPath, "usage_data.json");
        _settingsPath = Path.Combine(appDataPath, "tracking_settings.json");
    }

    public void Initialize(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
        LoadSettings();
        LoadData();
        StartTimers();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<TrackingSettingsFile>(json);
                if (loaded != null)
                {
                    _settings.Config = loaded.Config ?? new TrackingConfig();
                    _settings.Devices = loaded.Devices ?? new Dictionary<string, DeviceTrackingSetting>();
                }
            }
        }
        catch
        {
            // Silent fail: use defaults if settings cannot be loaded
            // This handles corrupted/invalid JSON gracefully
        }
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                var loaded = JsonSerializer.Deserialize<UsageDataFile>(json);
                if (loaded?.Records != null)
                {
                    foreach (var record in loaded.Records)
                    {
                        if (!_records.ContainsKey(record.DeviceId))
                            _records[record.DeviceId] = new Dictionary<string, DeviceUsageRecord>();
                        _records[record.DeviceId][record.DateKey] = record;
                    }
                }
            }
        }
        catch
        {
            // Silent fail: start fresh if data cannot be loaded
            // This handles corrupted/invalid JSON gracefully
        }
    }

    private void StartTimers()
    {
        // Update enabled time periodically
        _enabledTimeTimer = new Timer(_ =>
        {
            try { UpdateEnabledTime(); }
            catch { /* Timer callbacks must not throw to prevent silent termination */ }
        }, null, TimeSpan.FromSeconds(ENABLED_TIME_UPDATE_INTERVAL_SECONDS), TimeSpan.FromSeconds(ENABLED_TIME_UPDATE_INTERVAL_SECONDS));

        // Save data periodically
        _saveTimer = new Timer(_ =>
        {
            try { SaveData(); }
            catch { /* Timer callbacks must not throw to prevent silent termination */ }
        }, null, TimeSpan.FromMinutes(DATA_SAVE_INTERVAL_MINUTES), TimeSpan.FromMinutes(DATA_SAVE_INTERVAL_MINUTES));
    }

    public bool IsTracking(string deviceId)
    {
        lock (_lock)
        {
            return IsTrackingInternal(deviceId);
        }
    }

    /// <summary>
    /// Internal lock-free version for use within locked sections.
    /// Must only be called when already holding _lock.
    /// </summary>
    private bool IsTrackingInternal(string deviceId)
    {
        return _settings.Devices.TryGetValue(deviceId, out var setting) && setting.IsTrackingEnabled;
    }

    public void SetTracking(string deviceId, bool enabled)
    {
        TrackingSettingsFile settingsCopy;
        lock (_lock)
        {
            if (!_settings.Devices.ContainsKey(deviceId))
            {
                _settings.Devices[deviceId] = new DeviceTrackingSetting { DeviceId = deviceId };
            }

            var setting = _settings.Devices[deviceId];
            var wasEnabled = setting.IsTrackingEnabled;
            setting.IsTrackingEnabled = enabled;

            if (enabled && !wasEnabled && setting.FirstTrackedDate == null)
            {
                setting.FirstTrackedDate = DateTime.Today;
            }

            // Copy settings for I/O outside lock
            settingsCopy = new TrackingSettingsFile
            {
                Config = _settings.Config,
                Devices = new Dictionary<string, DeviceTrackingSetting>(_settings.Devices)
            };
        }

        SaveSettings(settingsCopy);

        TrackingChanged?.Invoke(this, EventArgs.Empty);
    }

    public DateTime? GetFirstTrackedDate(string deviceId)
    {
        lock (_lock)
        {
            return _settings.Devices.TryGetValue(deviceId, out var setting)
                ? setting.FirstTrackedDate
                : null;
        }
    }

    public void SetRetentionDays(int days)
    {
        TrackingSettingsFile settingsCopy;
        lock (_lock)
        {
            _settings.Config.RetentionDays = Math.Clamp(days, 7, 365);
            // Copy settings for I/O outside lock
            settingsCopy = new TrackingSettingsFile
            {
                Config = _settings.Config,
                Devices = new Dictionary<string, DeviceTrackingSetting>(_settings.Devices)
            };
        }
        SaveSettings(settingsCopy);
    }

    public void RegisterDeviceHandle(IntPtr handle, string deviceId)
    {
        lock (_lock)
        {
            _handleToDeviceId[handle] = deviceId;
        }
    }

    public void ProcessActiveInput(IntPtr deviceHandle)
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (!_handleToDeviceId.TryGetValue(deviceHandle, out var deviceId))
                return;

            // Use IsTrackingInternal to avoid re-acquiring lock
            if (!IsTrackingInternal(deviceId))
                return;

            var now = DateTime.Now;
            var today = DateTime.Today;
            var dateKey = today.ToString("yyyy-MM-dd");

            if (!_records.ContainsKey(deviceId))
                _records[deviceId] = new Dictionary<string, DeviceUsageRecord>();

            if (!_records[deviceId].ContainsKey(dateKey))
            {
                _records[deviceId][dateKey] = new DeviceUsageRecord
                {
                    DeviceId = deviceId,
                    Date = today
                };
            }

            // Calculate actual time elapsed since last active input
            if (_lastActiveTime.TryGetValue(deviceId, out var lastTime))
            {
                var elapsedMs = (now - lastTime).TotalMilliseconds;

                // Only count if within reasonable threshold (e.g., less than 1 second gap)
                // This prevents counting large gaps when user was away
                if (elapsedMs > 0 && elapsedMs < 1000)
                {
                    // Add the elapsed time in seconds (with 0.1s precision)
                    _records[deviceId][dateKey].ActiveSeconds += elapsedMs / 1000.0;
                }
            }

            // Update last active time
            _lastActiveTime[deviceId] = now;
        }
    }

    private void UpdateEnabledTime()
    {
        // Check disposed flag first to avoid accessing disposed resources
        if (_disposed || _deviceManager == null) return;

        var today = DateTime.Today;
        var dateKey = today.ToString("yyyy-MM-dd");

        lock (_lock)
        {
            foreach (var device in _deviceManager.Devices)
            {
                if (!IsTrackingInternal(device.DeviceId) || !device.IsEnabled)
                    continue;

                if (!_records.ContainsKey(device.DeviceId))
                    _records[device.DeviceId] = new Dictionary<string, DeviceUsageRecord>();

                if (!_records[device.DeviceId].ContainsKey(dateKey))
                {
                    _records[device.DeviceId][dateKey] = new DeviceUsageRecord
                    {
                        DeviceId = device.DeviceId,
                        Date = today
                    };
                }

                _records[device.DeviceId][dateKey].EnabledSeconds += ENABLED_TIME_UPDATE_INTERVAL_SECONDS;
            }
        }
    }

    public List<DeviceUsageRecord> GetUsageHistory(string deviceId, int days = 30)
    {
        var result = new List<DeviceUsageRecord>();
        var cutoff = DateTime.Today.AddDays(-days);

        lock (_lock)
        {
            if (_records.TryGetValue(deviceId, out var deviceRecords))
            {
                result.AddRange(deviceRecords.Values
                    .Where(r => r.Date >= cutoff)
                    .OrderByDescending(r => r.Date));
            }
        }

        return result;
    }

    public List<string> GetAllTrackingDevices()
    {
        lock (_lock)
        {
            return GetAllTrackingDevicesInternal();
        }
    }

    /// <summary>
    /// Internal lock-free version for use within locked sections.
    /// Must only be called when already holding _lock.
    /// </summary>
    private List<string> GetAllTrackingDevicesInternal()
    {
        return _settings.Devices
            .Where(kv => kv.Value.IsTrackingEnabled)
            .Select(kv => kv.Key)
            .ToList();
    }

    public (double ActiveSeconds, long EnabledSeconds) GetTodayTotals()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        double totalActive = 0;
        long totalEnabled = 0;

        lock (_lock)
        {
            foreach (var deviceId in GetAllTrackingDevicesInternal())
            {
                if (_records.TryGetValue(deviceId, out var deviceRecords) &&
                    deviceRecords.TryGetValue(today, out var record))
                {
                    totalActive += record.ActiveSeconds;
                    totalEnabled += record.EnabledSeconds;
                }
            }
        }

        return (totalActive, totalEnabled);
    }

    public void CleanupOldData()
    {
        lock (_lock)
        {
            var cutoff = DateTime.Today.AddDays(-_settings.Config.RetentionDays);
            var keysToRemove = new List<string>();
            foreach (var deviceRecords in _records.Values)
            {
                var datesToRemove = deviceRecords.Keys
                    .Where(k => DateTime.TryParse(k, out var d) && d < cutoff)
                    .ToList();

                foreach (var date in datesToRemove)
                    deviceRecords.Remove(date);
            }
        }
    }

    private void SaveSettings(TrackingSettingsFile settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silent fail: settings persistence failure should not crash the application
            // Settings will be lost for this session but will be recreated next time
        }
    }

    public void SaveData()
    {
        // Copy data inside lock, perform I/O outside lock
        List<DeviceUsageRecord> allRecords;
        lock (_lock)
        {
            allRecords = new List<DeviceUsageRecord>();
            foreach (var deviceRecords in _records.Values)
                allRecords.AddRange(deviceRecords.Values);
        }

        try
        {
            var dir = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var file = new UsageDataFile { Records = allRecords };
            var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }
        catch
        {
            // Silent fail: data persistence failure should not crash the application
            // Data will be lost for this session but tracking continues in memory
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop timers first before disposing resources they depend on
        _enabledTimeTimer?.Dispose();
        _saveTimer?.Dispose();
        _enabledTimeTimer = null;
        _saveTimer = null;

        SaveData();
    }

    /// <summary>
    /// Clears all device handle mappings. Should be called before re-enumerating devices.
    /// </summary>
    public void ClearDeviceHandles()
    {
        lock (_lock)
        {
            _handleToDeviceId.Clear();
        }
    }
}
