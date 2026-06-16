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

    private readonly string _dataPath;
    private readonly string _settingsPath;
    private readonly Dictionary<string, Dictionary<string, DeviceUsageRecord>> _records = new();
    private readonly TrackingSettingsFile _settings = new();
    private readonly object _lock = new();

    private Timer? _enabledTimeTimer;
    private Timer? _saveTimer;
    private DeviceManager? _deviceManager;

    // Handle to device ID mapping (populated from DeviceManager)
    private readonly Dictionary<IntPtr, string> _handleToDeviceId = new();

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
            // Use defaults on error
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
            // Start fresh on error
        }
    }

    private void StartTimers()
    {
        // Update enabled time every 60 seconds
        _enabledTimeTimer = new Timer(_ => UpdateEnabledTime(), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

        // Save data every 5 minutes
        _saveTimer = new Timer(_ => SaveData(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool IsTracking(string deviceId)
    {
        lock (_lock)
        {
            return _settings.Devices.TryGetValue(deviceId, out var setting) && setting.IsTrackingEnabled;
        }
    }

    public void SetTracking(string deviceId, bool enabled)
    {
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

            SaveSettings();
        }

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
        lock (_lock)
        {
            _settings.Config.RetentionDays = Math.Clamp(days, 7, 365);
            SaveSettings();
        }
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
        string? deviceId;
        lock (_lock)
        {
            if (!_handleToDeviceId.TryGetValue(deviceHandle, out deviceId))
                return;
        }

        if (!IsTracking(deviceId))
            return;

        var today = DateTime.Today;
        var dateKey = today.ToString("yyyy-MM-dd");

        lock (_lock)
        {
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

            _records[deviceId][dateKey].ActiveSeconds++;
        }
    }

    private void UpdateEnabledTime()
    {
        if (_deviceManager == null) return;

        var today = DateTime.Today;
        var dateKey = today.ToString("yyyy-MM-dd");

        lock (_lock)
        {
            foreach (var device in _deviceManager.Devices)
            {
                if (!IsTracking(device.DeviceId) || !device.IsEnabled)
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

                _records[device.DeviceId][dateKey].EnabledSeconds += 60;
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
            return _settings.Devices
                .Where(kv => kv.Value.IsTrackingEnabled)
                .Select(kv => kv.Key)
                .ToList();
        }
    }

    public (long ActiveSeconds, long EnabledSeconds) GetTodayTotals()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        long totalActive = 0;
        long totalEnabled = 0;

        lock (_lock)
        {
            foreach (var deviceId in GetAllTrackingDevices())
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
        var cutoff = DateTime.Today.AddDays(-_settings.Config.RetentionDays);

        lock (_lock)
        {
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

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silent fail
        }
    }

    public void SaveData()
    {
        try
        {
            var dir = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var allRecords = new List<DeviceUsageRecord>();
            lock (_lock)
            {
                foreach (var deviceRecords in _records.Values)
                    allRecords.AddRange(deviceRecords.Values);
            }

            var file = new UsageDataFile { Records = allRecords };
            var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }
        catch
        {
            // Silent fail
        }
    }

    public void Dispose()
    {
        _enabledTimeTimer?.Dispose();
        _saveTimer?.Dispose();
        SaveData();
    }
}
