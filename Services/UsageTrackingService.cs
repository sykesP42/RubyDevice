using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using RubyDevice.Core;
using RubyDevice.Models;

namespace RubyDevice.Services;

/// <summary>
/// Service for tracking device usage time (enabled and active duration).
/// Records daily usage per device, persists to %AppData%\RubyDevice\,
/// and exposes aggregation (weekly/monthly) and activity-rate queries.
/// Thread-safe: all state is guarded by a lock.
/// </summary>
public class UsageTrackingService : IDisposable
{
    private static UsageTrackingService? _instance;

    /// <summary>
    /// Gets the singleton UsageTrackingService instance
    /// </summary>
    public static UsageTrackingService Instance => _instance ??= new UsageTrackingService();

    // Constants for timer intervals
    private const int ENABLED_TIME_UPDATE_INTERVAL_SECONDS = 60;
    private const int DATA_SAVE_INTERVAL_MINUTES = 5;
    // Max interval credited per enabled-time tick; guards against inflated
    // counts when the process was suspended between ticks (e.g. sleep).
    private const int MAX_ENABLED_TIME_CREDIT_SECONDS = ENABLED_TIME_UPDATE_INTERVAL_SECONDS * 3;

    private readonly string _dataPath;
    private readonly string _settingsPath;
    private readonly Dictionary<string, Dictionary<string, DeviceUsageRecord>> _records = new();
    private readonly TrackingSettingsFile _settings = new();
    private readonly object _lock = new();

    private Timer? _enabledTimeTimer;
    private Timer? _saveTimer;
    private DeviceManager? _deviceManager;
    private bool _disposed;
    private DateTime _lastEnabledUpdateUtc = DateTime.UtcNow;
    private DateTime _lastCleanupDate = DateTime.Today;

    // Track last active time for each device (for accurate ActiveSeconds calculation)
    private readonly Dictionary<string, DateTime> _lastActiveTime = new();

    // Input gaps longer than this are treated as idle and not counted as active time
    private const double ACTIVE_IDLE_GAP_SECONDS = 2.0;

    /// <summary>
    /// Current tracking configuration, shared across all devices
    /// </summary>
    public TrackingConfig Config => _settings.Config;

    /// <summary>
    /// Raised when a device is added to or removed from usage tracking
    /// </summary>
    public event EventHandler? TrackingChanged;

    private UsageTrackingService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RubyDevice");
        _dataPath = Path.Combine(appDataPath, "usage_data.json");
        _settingsPath = Path.Combine(appDataPath, "tracking_settings.json");
    }

    /// <summary>
    /// Attaches the service to a device manager, loads persisted settings and
    /// usage data, drops records past the retention window, and starts the
    /// tracking timers
    /// </summary>
    public void Initialize(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;

        // Active time is accumulated from the device manager's activity events,
        // which already carry the resolved device identity.
        deviceManager.DeviceActivity += OnDeviceActivity;

        LoadSettings();
        LoadData();
        CleanupOldData();
        StartTimers();
    }

    private void OnDeviceActivity(object? sender, DeviceManager.DeviceActivityEventArgs e)
    {
        ProcessActiveInput(e.DeviceId);
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
            try
            {
                SaveData();
                // Drop expired records once per day
                if (DateTime.Today > _lastCleanupDate)
                {
                    _lastCleanupDate = DateTime.Today;
                    CleanupOldData();
                }
            }
            catch { /* Timer callbacks must not throw to prevent silent termination */ }
        }, null, TimeSpan.FromMinutes(DATA_SAVE_INTERVAL_MINUTES), TimeSpan.FromMinutes(DATA_SAVE_INTERVAL_MINUTES));
    }

    /// <summary>
    /// Checks whether usage tracking is enabled for a device
    /// </summary>
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

    /// <summary>
    /// Enables or disables usage tracking for a device and persists the setting
    /// </summary>
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

    /// <summary>
    /// Returns the date tracking began for a device, or null if it is not tracked
    /// </summary>
    public DateTime? GetFirstTrackedDate(string deviceId)
    {
        lock (_lock)
        {
            return _settings.Devices.TryGetValue(deviceId, out var setting)
                ? setting.FirstTrackedDate
                : null;
        }
    }

    /// <summary>
    /// Sets how many days of usage history to keep, clamped to the supported
    /// range of 7 to 365 days
    /// </summary>
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

    /// <summary>
    /// Records active input for a device, accumulating active seconds based on
    /// the time elapsed since the previous input. Gaps longer than
    /// <see cref="ACTIVE_IDLE_GAP_SECONDS"/> are treated as idle time.
    /// </summary>
    public void ProcessActiveInput(string deviceId)
    {
        if (_disposed || string.IsNullOrEmpty(deviceId)) return;

        lock (_lock)
        {
            if (!IsTrackingInternal(deviceId))
                return;

            var now = DateTime.Now;
            var today = DateTime.Today;
            var dateKey = today.ToString("yyyy-MM-dd");

            EnsureRecord(deviceId, dateKey, today);

            // Credit the gap since the previous input only when it is short
            // enough to count as continuous use
            if (_lastActiveTime.TryGetValue(deviceId, out var lastTime))
            {
                var elapsed = (now - lastTime).TotalSeconds;
                if (elapsed > 0 && elapsed <= ACTIVE_IDLE_GAP_SECONDS)
                {
                    _records[deviceId][dateKey].ActiveSeconds += elapsed;
                }
            }

            _lastActiveTime[deviceId] = now;
        }
    }

    /// <summary>
    /// Periodically accumulates enabled seconds for all tracked, enabled devices.
    /// Uses the real elapsed time between ticks so delayed or suspended ticks do
    /// not inflate the count.
    /// </summary>
    private void UpdateEnabledTime()
    {
        if (_disposed || _deviceManager == null) return;

        var nowUtc = DateTime.UtcNow;
        var devices = _deviceManager.GetDevicesSnapshot();

        lock (_lock)
        {
            var elapsed = (nowUtc - _lastEnabledUpdateUtc).TotalSeconds;
            _lastEnabledUpdateUtc = nowUtc;
            if (elapsed <= 0) return;
            elapsed = Math.Min(elapsed, MAX_ENABLED_TIME_CREDIT_SECONDS);

            var today = DateTime.Today;
            var dateKey = today.ToString("yyyy-MM-dd");

            foreach (var device in devices)
            {
                if (!IsTrackingInternal(device.DeviceId) || !device.IsEnabled)
                    continue;

                EnsureRecord(device.DeviceId, dateKey, today);
                _records[device.DeviceId][dateKey].EnabledSeconds += elapsed;
            }
        }
    }

    /// <summary>
    /// Makes sure a record exists for the device/date pair. Must be called while
    /// holding <see cref="_lock"/>.
    /// </summary>
    private void EnsureRecord(string deviceId, string dateKey, DateTime date)
    {
        if (!_records.TryGetValue(deviceId, out var deviceRecords))
        {
            deviceRecords = new Dictionary<string, DeviceUsageRecord>();
            _records[deviceId] = deviceRecords;
        }

        if (!deviceRecords.ContainsKey(dateKey))
        {
            deviceRecords[dateKey] = new DeviceUsageRecord
            {
                DeviceId = deviceId,
                Date = date
            };
        }
    }

    /// <summary>
    /// Gets usage history for a device within the given number of days,
    /// ordered most-recent-first
    /// </summary>
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

    /// <summary>
    /// Gets today's active/enabled seconds for a single device
    /// </summary>
    public (double ActiveSeconds, double EnabledSeconds) GetTodayUsage(string deviceId)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        lock (_lock)
        {
            if (_records.TryGetValue(deviceId, out var deviceRecords) &&
                deviceRecords.TryGetValue(today, out var record))
            {
                return (record.ActiveSeconds, record.EnabledSeconds);
            }
        }

        return (0, 0);
    }

    /// <summary>
    /// Returns the identifiers of every device currently being tracked
    /// </summary>
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

    /// <summary>
    /// Gets the combined active/enabled seconds for today across all tracked devices
    /// </summary>
    public (double ActiveSeconds, double EnabledSeconds) GetTodayTotals()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        double totalActive = 0;
        double totalEnabled = 0;

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

    /// <summary>
    /// Returns the all-time total of active seconds recorded for a device
    /// </summary>
    public double GetTotalActiveSeconds(string deviceId)
    {
        lock (_lock)
        {
            return _records.TryGetValue(deviceId, out var deviceRecords)
                ? deviceRecords.Values.Sum(r => r.ActiveSeconds)
                : 0;
        }
    }

    /// <summary>
    /// Drops usage records older than the configured retention period
    /// </summary>
    public void CleanupOldData()
    {
        lock (_lock)
        {
            var cutoff = DateTime.Today.AddDays(-_settings.Config.RetentionDays);
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

    /// <summary>
    /// Clears all usage records and per-device tracking settings, then persists
    /// the empty state to disk
    /// </summary>
    public void ClearAllData()
    {
        TrackingSettingsFile settingsCopy;
        lock (_lock)
        {
            _records.Clear();
            _lastActiveTime.Clear();
            _settings.Devices.Clear();
            settingsCopy = new TrackingSettingsFile
            {
                Config = _settings.Config,
                Devices = new Dictionary<string, DeviceTrackingSetting>(_settings.Devices)
            };
        }

        SaveSettings(settingsCopy);
        SaveData();
        TrackingChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Flushes all in-memory usage records to disk. File I/O happens outside the
    /// lock so tracking is not blocked while writing.
    /// </summary>
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

    /// <summary>
    /// Stops the tracking timers, saves pending data and settings, and releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_deviceManager != null)
        {
            _deviceManager.DeviceActivity -= OnDeviceActivity;
            _deviceManager = null;
        }

        // Stop timers first before disposing resources they depend on
        _enabledTimeTimer?.Dispose();
        _saveTimer?.Dispose();
        _enabledTimeTimer = null;
        _saveTimer = null;

        TrackingSettingsFile settingsCopy;
        lock (_lock)
        {
            settingsCopy = new TrackingSettingsFile
            {
                Config = _settings.Config,
                Devices = new Dictionary<string, DeviceTrackingSetting>(_settings.Devices)
            };
        }
        SaveSettings(settingsCopy);
        SaveData();
    }

    /// <summary>
    /// Get weekly aggregated usage for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="weeks">Number of weeks to retrieve (default 12)</param>
    /// <returns>List of weekly summaries, ordered by week start date (oldest first)</returns>
    public List<WeeklyUsageSummary> GetWeeklyUsage(string deviceId, int weeks = 12)
    {
        var result = new List<WeeklyUsageSummary>();
        var today = DateTime.Today;
        var cutoff = today.AddDays(-(weeks * 7));

        lock (_lock)
        {
            if (!_records.TryGetValue(deviceId, out var deviceRecords))
                return result;

            // Group records by week
            var weeklyGroups = deviceRecords.Values
                .Where(r => r.Date >= cutoff)
                .GroupBy(r => GetWeekStartDate(r.Date))
                .OrderBy(g => g.Key);

            foreach (var group in weeklyGroups)
            {
                var weekStart = group.Key;
                var weekEnd = weekStart.AddDays(6);
                var summary = new WeeklyUsageSummary
                {
                    WeekStartDate = weekStart,
                    WeekEndDate = weekEnd,
                    DeviceId = deviceId,
                    TotalActiveSeconds = group.Sum(r => r.ActiveSeconds),
                    TotalEnabledSeconds = group.Sum(r => r.EnabledSeconds),
                    DaysWithData = group.Count()
                };
                result.Add(summary);
            }
        }

        return result;
    }

    /// <summary>
    /// Get monthly aggregated usage for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="months">Number of months to retrieve (default 12)</param>
    /// <returns>List of monthly summaries, ordered by year/month (oldest first)</returns>
    public List<MonthlyUsageSummary> GetMonthlyUsage(string deviceId, int months = 12)
    {
        var result = new List<MonthlyUsageSummary>();
        var today = DateTime.Today;
        var cutoff = today.AddMonths(-months);

        lock (_lock)
        {
            if (!_records.TryGetValue(deviceId, out var deviceRecords))
                return result;

            // Group records by month
            var monthlyGroups = deviceRecords.Values
                .Where(r => r.Date >= cutoff)
                .GroupBy(r => new { r.Date.Year, r.Date.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month);

            foreach (var group in monthlyGroups)
            {
                var summary = new MonthlyUsageSummary
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    DeviceId = deviceId,
                    TotalActiveSeconds = group.Sum(r => r.ActiveSeconds),
                    TotalEnabledSeconds = group.Sum(r => r.EnabledSeconds),
                    DaysWithData = group.Count()
                };
                result.Add(summary);
            }
        }

        return result;
    }

    /// <summary>
    /// Get usage history for multiple devices for comparison
    /// </summary>
    /// <param name="deviceIds">List of device identifiers</param>
    /// <param name="days">Number of days to retrieve</param>
    /// <returns>Dictionary mapping device ID to its usage records</returns>
    public Dictionary<string, List<DeviceUsageRecord>> GetMultiDeviceHistory(List<string> deviceIds, int days = 30)
    {
        var result = new Dictionary<string, List<DeviceUsageRecord>>();
        var cutoff = DateTime.Today.AddDays(-days);

        lock (_lock)
        {
            foreach (var deviceId in deviceIds)
            {
                if (_records.TryGetValue(deviceId, out var deviceRecords))
                {
                    result[deviceId] = deviceRecords.Values
                        .Where(r => r.Date >= cutoff)
                        .OrderBy(r => r.Date)
                        .ToList();
                }
                else
                {
                    result[deviceId] = new List<DeviceUsageRecord>();
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate activity rate (ActiveSeconds / EnabledSeconds) for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="days">Number of days to analyze (default 7)</param>
    /// <returns>Activity rate as percentage (0-100), or 0 if no enabled time</returns>
    public double CalculateActivityRate(string deviceId, int days = 7)
    {
        var records = GetUsageHistory(deviceId, days);
        if (records.Count == 0)
            return 0;

        var totalActive = records.Sum(r => r.ActiveSeconds);
        var totalEnabled = records.Sum(r => r.EnabledSeconds);

        if (totalEnabled == 0)
            return 0;

        return (totalActive / totalEnabled) * 100;
    }

    /// <summary>
    /// Get the Monday date for the week containing the given date
    /// </summary>
    private static DateTime GetWeekStartDate(DateTime date)
    {
        // Monday-based week (ISO 8601)
        var dayOfWeek = date.DayOfWeek;
        var daysToSubtract = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return date.AddDays(-daysToSubtract).Date;
    }
}
