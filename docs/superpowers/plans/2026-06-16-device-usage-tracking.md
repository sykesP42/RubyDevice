# Device Usage Tracking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional device usage tracking that records enabled and active time per device on a daily basis, with user control over which devices to track.

**Architecture:** Create a new `UsageTrackingService` that integrates with the existing `DeviceManager` for active input detection. Add a new `UsageRecordPage` in the navigation. Data stored in JSON files under `%AppData%\RubyDevice\`.

**Tech Stack:** WinUI 3, .NET 9, C#, System.Text.Json

---

## File Structure

**New Files:**
- `Services/UsageTrackingService.cs` - Core tracking service
- `Models/DeviceUsageRecord.cs` - Data model for usage records
- `Models/TrackingSettings.cs` - Data model for tracking configuration
- `Pages/UsageRecordPage.xaml` - XAML for the new page
- `Pages/UsageRecordPage.xaml.cs` - Code-behind for the page

**Modified Files:**
- `Services/LocalizationService.cs` - Add new localized strings
- `Core/DeviceManager.cs` - Add integration with tracking service
- `MainWindow.xaml` - Add new navigation item
- `MainWindow.xaml.cs` - Handle navigation and initialization
- `ViewModels/MainViewModel.cs` - Add tracking-related properties

---

## Task 1: Create Data Models

**Files:**
- Create: `Models/DeviceUsageRecord.cs`
- Create: `Models/TrackingSettings.cs`

- [ ] **Step 1: Create Models directory and DeviceUsageRecord model**

Create file `Models/DeviceUsageRecord.cs`:

```csharp
using System;

namespace RubyDevice.Models;

/// <summary>
/// Single day's usage record for one device
/// </summary>
public class DeviceUsageRecord
{
    public string DeviceId { get; set; } = "";
    public DateTime Date { get; set; }
    public long ActiveSeconds { get; set; }
    public long EnabledSeconds { get; set; }

    /// <summary>
    /// Date key for dictionary storage (YYYY-MM-DD format)
    /// </summary>
    public string DateKey => Date.ToString("yyyy-MM-dd");
}

/// <summary>
/// Collection of usage records for JSON serialization
/// </summary>
public class UsageDataFile
{
    public System.Collections.Generic.List<DeviceUsageRecord> Records { get; set; } = new();
}
```

- [ ] **Step 2: Create TrackingSettings model**

Create file `Models/TrackingSettings.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace RubyDevice.Models;

/// <summary>
/// Per-device tracking configuration
/// </summary>
public class DeviceTrackingSetting
{
    public string DeviceId { get; set; } = "";
    public bool IsTrackingEnabled { get; set; }
    public DateTime? FirstTrackedDate { get; set; }
}

/// <summary>
/// Global tracking configuration
/// </summary>
public class TrackingConfig
{
    public int RetentionDays { get; set; } = 30;
    public bool AutoCleanup { get; set; } = true;
}

/// <summary>
/// Complete tracking settings file structure
/// </summary>
public class TrackingSettingsFile
{
    public TrackingConfig Config { get; set; } = new();
    public Dictionary<string, DeviceTrackingSetting> Devices { get; set; } = new();
}
```

- [ ] **Step 3: Build to verify models compile**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add Models/DeviceUsageRecord.cs Models/TrackingSettings.cs
git commit -m "feat: add data models for usage tracking"
```

---

## Task 2: Create UsageTrackingService

**Files:**
- Create: `Services/UsageTrackingService.cs`

- [ ] **Step 1: Create UsageTrackingService skeleton**

Create file `Services/UsageTrackingService.cs`:

```csharp
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
```

- [ ] **Step 2: Build to verify service compiles**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 3: Commit**

```bash
git add Services/UsageTrackingService.cs
git commit -m "feat: add UsageTrackingService for device usage tracking"
```

---

## Task 3: Add Localization Strings

**Files:**
- Modify: `Services/LocalizationService.cs`

- [ ] **Step 1: Add new English strings**

Add the following keys to `EnglishStrings` dictionary in `Services/LocalizationService.cs` (after line 161, before the closing brace):

```csharp
        // Usage Record Page
        ["NavUsageRecord"] = "Usage Record",
        ["HeaderUsageRecord"] = "Usage Record",
        ["HeaderUsageRecordDesc"] = "Track device usage time",
        ["TrackingEnabled"] = "Tracking Enabled",
        ["TrackingDisabled"] = "Tracking Disabled",
        ["TrackedDevices"] = "Tracked Devices",
        ["TodayUsage"] = "Today's Usage",
        ["ActiveTime"] = "Active Time",
        ["EnabledTime"] = "Enabled Time",
        ["RetentionDays"] = "Data Retention Days",
        ["AutoCleanup"] = "Auto Cleanup",
        ["NoTrackingDevices"] = "No devices are being tracked",
        ["EnableTrackingHint"] = "Enable tracking on a device to start recording",
        ["Last7Days"] = "Last 7 Days",
        ["Last30Days"] = "Last 30 Days",
        ["AllTime"] = "All Time",
        ["SelectDevice"] = "Select Device",
        ["UsageHistory"] = "Usage History",
        ["Date"] = "Date",
        ["TrackedSince"] = "Tracked since {0}",
```

- [ ] **Step 2: Add new Chinese strings**

Add the following keys to `ChineseStrings` dictionary in `Services/LocalizationService.cs` (after line 286, before the closing brace):

```csharp
        // Usage Record Page
        ["NavUsageRecord"] = "使用记录",
        ["HeaderUsageRecord"] = "使用记录",
        ["HeaderUsageRecordDesc"] = "追踪设备使用时间",
        ["TrackingEnabled"] = "已开启追踪",
        ["TrackingDisabled"] = "未开启追踪",
        ["TrackedDevices"] = "追踪中的设备",
        ["TodayUsage"] = "今日使用",
        ["ActiveTime"] = "活跃时长",
        ["EnabledTime"] = "启用时长",
        ["RetentionDays"] = "数据保留天数",
        ["AutoCleanup"] = "自动清理",
        ["NoTrackingDevices"] = "暂无追踪中的设备",
        ["EnableTrackingHint"] = "开启设备追踪以开始记录",
        ["Last7Days"] = "最近 7 天",
        ["Last30Days"] = "最近 30 天",
        ["AllTime"] = "全部时间",
        ["SelectDevice"] = "选择设备",
        ["UsageHistory"] = "使用历史",
        ["Date"] = "日期",
        ["TrackedSince"] = "自 {0} 开始追踪",
```

- [ ] **Step 3: Build to verify strings compile**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add Services/LocalizationService.cs
git commit -m "feat: add localization strings for usage tracking page"
```

---

## Task 4: Integrate UsageTrackingService with DeviceManager

**Files:**
- Modify: `Core/DeviceManager.cs`

- [ ] **Step 1: Add method to register device handles with tracking service**

In `Core/DeviceManager.cs`, add this line at the end of the `RefreshDevices()` method (after line 293, before `Devices.Add(info)`):

```csharp
                        // Register device handle with tracking service
                        Services.UsageTrackingService.Instance.RegisterDeviceHandle(dev.hDevice, info.DeviceId);
```

- [ ] **Step 2: Add ProcessActiveInput call in ProcessRawInput**

In `Core/DeviceManager.cs`, modify the `ProcessRawInput` method. Find the line:
```csharp
_currentInputDevice = raw.header.hDevice;
```

Add after it:
```csharp
                    // Notify tracking service of active input
                    Services.UsageTrackingService.Instance.ProcessActiveInput(raw.header.hDevice);
```

The modified method should look like:
```csharp
    public void ProcessRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        GetRawInputData(hRawInput, 0x10000003, IntPtr.Zero, ref size, Marshal.SizeOf<RAWINPUTHEADER>());

        if (size > 0)
        {
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, 0x10000003, buffer, ref size, Marshal.SizeOf<RAWINPUTHEADER>()) > 0)
                {
                    RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                    _currentInputDevice = raw.header.hDevice;

                    // Notify tracking service of active input
                    Services.UsageTrackingService.Instance.ProcessActiveInput(raw.header.hDevice);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
```

- [ ] **Step 3: Build to verify integration compiles**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add Core/DeviceManager.cs
git commit -m "feat: integrate UsageTrackingService with DeviceManager"
```

---

## Task 5: Create UsageRecordPage UI

**Files:**
- Create: `Pages/UsageRecordPage.xaml`
- Create: `Pages/UsageRecordPage.xaml.cs`

- [ ] **Step 1: Create UsageRecordPage XAML**

Create file `Pages/UsageRecordPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Page x:Class="RubyDevice.Pages.UsageRecordPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:RubyDevice.ViewModels"
    Background="{StaticResource WindowBackgroundBrush}">

    <ScrollViewer Padding="24,16">
        <StackPanel Spacing="20">
            <!-- Device Tracking Controls -->
            <Border Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="20"
                   BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                <StackPanel Spacing="16">
                    <TextBlock x:Name="TextTrackedDevices" FontSize="18" FontWeight="SemiBold"
                              Foreground="{StaticResource TextPrimaryBrush}"/>

                    <TextBlock x:Name="TextNoTracking" FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"
                              Visibility="Collapsed"/>

                    <ListView x:Name="DeviceList" SelectionMode="None">
                        <ListView.ItemTemplate>
                            <DataTemplate x:DataType="vm:DeviceViewModel">
                                <Grid Padding="12,8" ColumnSpacing="16" Background="{StaticResource DividerBrush}"
                                     CornerRadius="8" Margin="0,4">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>

                                    <!-- Type Icon -->
                                    <Border Grid.Column="0" Width="32" Height="32" CornerRadius="6"
                                           Background="{StaticResource CardBackgroundBrush}">
                                        <TextBlock Text="{x:Bind IconGlyph}" FontFamily="Segoe MDL2 Assets"
                                                  FontSize="14" HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Foreground="{StaticResource TextSecondaryBrush}"/>
                                    </Border>

                                    <!-- Device Name -->
                                    <StackPanel Grid.Column="1" Spacing="2">
                                        <TextBlock Text="{x:Bind Name}" FontSize="13" FontWeight="Medium"
                                                  Foreground="{StaticResource TextPrimaryBrush}"/>
                                        <TextBlock Text="{x:Bind TypeName}" FontSize="11"
                                                  Foreground="{StaticResource TextSecondaryBrush}"/>
                                    </StackPanel>

                                    <!-- Tracking Toggle -->
                                    <ToggleSwitch Grid.Column="2" IsOn="{x:Bind IsTracking, Mode=TwoWay}"
                                                 Toggled="TrackingToggle_Toggled" Tag="{x:Bind DeviceId}"/>
                                </Grid>
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                </StackPanel>
            </Border>

            <!-- Today's Usage -->
            <Grid ColumnSpacing="16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <!-- Tracked Count -->
                <Border Grid.Column="0" Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="16"
                       BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                    <StackPanel Spacing="8">
                        <Border Width="40" Height="40" CornerRadius="8" Background="{StaticResource PrimaryLightBrush}">
                            <FontIcon Glyph="&#xE9D9;" FontSize="18" Foreground="{StaticResource PrimaryBrush}"/>
                        </Border>
                        <TextBlock x:Name="TextTrackedCount" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"/>
                        <TextBlock x:Name="CountTracked" Text="0" FontSize="28" FontWeight="Bold"
                                  Foreground="{StaticResource TextPrimaryBrush}"/>
                    </StackPanel>
                </Border>

                <!-- Active Time -->
                <Border Grid.Column="1" Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="16"
                       BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                    <StackPanel Spacing="8">
                        <Border Width="40" Height="40" CornerRadius="8" Background="#D1FAE5">
                            <FontIcon Glyph="&#xE823;" FontSize="18" Foreground="{StaticResource SuccessBrush}"/>
                        </Border>
                        <TextBlock x:Name="TextActiveTime" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"/>
                        <TextBlock x:Name="ValueActiveTime" Text="0m" FontSize="28" FontWeight="Bold"
                                  Foreground="{StaticResource TextPrimaryBrush}"/>
                    </StackPanel>
                </Border>

                <!-- Enabled Time -->
                <Border Grid.Column="2" Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="16"
                       BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                    <StackPanel Spacing="8">
                        <Border Width="40" Height="40" CornerRadius="8" Background="#DBEAFE">
                            <FontIcon Glyph="&#xE823;" FontSize="18" Foreground="{StaticResource PrimaryBrush}"/>
                        </Border>
                        <TextBlock x:Name="TextEnabledTime" FontSize="12" Foreground="{StaticResource TextSecondaryBrush}"/>
                        <TextBlock x:Name="ValueEnabledTime" Text="0m" FontSize="28" FontWeight="Bold"
                                  Foreground="{StaticResource TextPrimaryBrush}"/>
                    </StackPanel>
                </Border>
            </Grid>

            <!-- Settings -->
            <Border Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="20"
                   BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                <StackPanel Spacing="16">
                    <TextBlock x:Name="TextSettings" FontSize="18" FontWeight="SemiBold"
                              Foreground="{StaticResource TextPrimaryBrush}"/>

                    <Grid ColumnSpacing="16">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>

                        <TextBlock Grid.Column="0" x:Name="TextRetentionDays" FontSize="13"
                                  Foreground="{StaticResource TextPrimaryBrush}" VerticalAlignment="Center"/>
                        <NumberBox Grid.Column="1" x:Name="RetentionBox" Value="30" Minimum="7" Maximum="365"
                                   SpinButtonPlacementMode="Compact" Width="100"
                                   ValueChanged="RetentionBox_ValueChanged"/>
                    </Grid>
                </StackPanel>
            </Border>

            <!-- Usage History -->
            <Border Background="{StaticResource CardBackgroundBrush}" CornerRadius="12" Padding="20"
                   BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                <StackPanel Spacing="16">
                    <Grid ColumnSpacing="12">
                        <TextBlock x:Name="TextUsageHistory" FontSize="18" FontWeight="SemiBold"
                                  Foreground="{StaticResource TextPrimaryBrush}"/>

                        <ComboBox Grid.Column="1" x:Name="DeviceSelector" HorizontalAlignment="Right"
                                 Width="200" SelectionChanged="DeviceSelector_SelectionChanged"/>
                    </Grid>

                    <!-- Time Range Selector -->
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <RadioButton x:Name="Range7Days" Content="7" Tag="7" Checked="Range_Changed"/>
                        <RadioButton x:Name="Range30Days" Content="30" Tag="30" Checked="Range_Changed" IsChecked="True"/>
                        <RadioButton x:Name="RangeAll" Content="All" Tag="0" Checked="Range_Changed"/>
                    </StackPanel>

                    <!-- Chart Area -->
                    <Canvas x:Name="ChartCanvas" Height="150" Background="{StaticResource DividerBrush}" CornerRadius="8"/>

                    <!-- Data List -->
                    <ListView x:Name="HistoryList" SelectionMode="None" MaxHeight="200">
                        <ListView.ItemTemplate>
                            <DataTemplate>
                                <Grid Padding="12,8" ColumnSpacing="16" Background="{StaticResource DividerBrush}"
                                     CornerRadius="8" Margin="0,4">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>

                                    <TextBlock Grid.Column="0" Text="{Binding DateText}" FontSize="13"
                                              Foreground="{StaticResource TextPrimaryBrush}"/>
                                    <TextBlock Grid.Column="1" Text="{Binding ActiveText}" FontSize="12"
                                              Foreground="{StaticResource SuccessBrush}"/>
                                    <TextBlock Grid.Column="2" Text="{Binding EnabledText}" FontSize="12"
                                              Foreground="{StaticResource TextSecondaryBrush}"/>
                                </Grid>
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 2: Create UsageRecordPage code-behind**

Create file `Pages/UsageRecordPage.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using RubyDevice.Core;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class UsageRecordPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private int _selectedDays = 30;
    private string? _selectedDeviceId;

    public UsageRecordPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        TextTrackedDevices.Text = _loc["TrackedDevices"];
        TextNoTracking.Text = _loc["EnableTrackingHint"];
        TextTrackedCount.Text = _loc["TrackedDevices"];
        TextActiveTime.Text = _loc["ActiveTime"];
        TextEnabledTime.Text = _loc["EnabledTime"];
        TextSettings.Text = _loc["Behavior"];
        TextRetentionDays.Text = _loc["RetentionDays"];
        TextUsageHistory.Text = _loc["UsageHistory"];

        Range7Days.Content = _loc["Last7Days"];
        Range30Days.Content = _loc["Last30Days"];
        RangeAll.Content = _loc["AllTime"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        UpdateDeviceList();
        UpdateStats();
        UpdateHistory();

        UsageTrackingService.Instance.TrackingChanged += OnTrackingChanged;

        // Set retention days
        RetentionBox.Value = UsageTrackingService.Instance.Config.RetentionDays;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        UsageTrackingService.Instance.TrackingChanged -= OnTrackingChanged;
    }

    private void OnTrackingChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateDeviceList();
            UpdateStats();
        });
    }

    private void UpdateDeviceList()
    {
        if (_viewModel == null) return;

        DeviceList.ItemsSource = _viewModel.AllDevices;

        // Check if any device is being tracked
        var hasTracking = _viewModel.AllDevices.Any(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        TextNoTracking.Visibility = hasTracking ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateStats()
    {
        if (_viewModel == null) return;

        var trackedCount = _viewModel.AllDevices.Count(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        CountTracked.Text = trackedCount.ToString();

        var (activeSeconds, enabledSeconds) = UsageTrackingService.Instance.GetTodayTotals();
        ValueActiveTime.Text = FormatTime(activeSeconds);
        ValueEnabledTime.Text = FormatTime(enabledSeconds);
    }

    private void UpdateHistory()
    {
        if (_viewModel == null) return;

        // Populate device selector with tracked devices
        var trackedDevices = _viewModel.AllDevices
            .Where(d => UsageTrackingService.Instance.IsTracking(d.DeviceId))
            .ToList();

        DeviceSelector.ItemsSource = trackedDevices;
        DeviceSelector.DisplayMemberPath = "Name";

        if (trackedDevices.Count > 0)
        {
            if (_selectedDeviceId == null || !trackedDevices.Any(d => d.DeviceId == _selectedDeviceId))
            {
                DeviceSelector.SelectedIndex = 0;
                _selectedDeviceId = trackedDevices[0].DeviceId;
            }
        }

        LoadHistoryData();
    }

    private void LoadHistoryData()
    {
        if (string.IsNullOrEmpty(_selectedDeviceId)) return;

        var days = _selectedDays == 0 ? 365 : _selectedDays;
        var history = UsageTrackingService.Instance.GetUsageHistory(_selectedDeviceId, days);

        // Update list
        var displayItems = history.Select(r => new
        {
            DateText = r.Date.ToString("yyyy-MM-dd"),
            ActiveText = FormatTime(r.ActiveSeconds),
            EnabledText = FormatTime(r.EnabledSeconds)
        }).ToList();

        HistoryList.ItemsSource = displayItems;

        // Update chart
        DrawChart(history);
    }

    private void DrawChart(List<DeviceUsageRecord> records)
    {
        ChartCanvas.Children.Clear();

        if (records.Count == 0) return;

        var maxSeconds = records.Max(r => Math.Max(r.ActiveSeconds, 1));
        var barWidth = Math.Max(10, (ChartCanvas.ActualWidth - 40) / records.Count - 2);
        var maxBarHeight = ChartCanvas.ActualHeight - 20;

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[records.Count - 1 - i]; // Reverse to show oldest first
            var barHeight = (record.ActiveSeconds / (double)maxSeconds) * maxBarHeight;
            var x = 20 + i * (barWidth + 2);
            var y = maxBarHeight - barHeight;

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, barHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y + 10);
            ChartCanvas.Children.Add(rect);
        }
    }

    private static string FormatTime(long seconds)
    {
        var hours = seconds / 3600;
        var mins = (seconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private void TrackingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.Tag is string deviceId)
        {
            UsageTrackingService.Instance.SetTracking(deviceId, toggle.IsOn);

            // Update the DeviceViewModel
            var device = _viewModel?.AllDevices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device != null)
            {
                device.IsTracking = toggle.IsOn;
            }
        }
    }

    private void RetentionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (args.NewValue >= 7 && args.NewValue <= 365)
        {
            UsageTrackingService.Instance.SetRetentionDays((int)args.NewValue);
        }
    }

    private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceSelector.SelectedItem is DeviceViewModel device)
        {
            _selectedDeviceId = device.DeviceId;
            LoadHistoryData();
        }
    }

    private void Range_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _selectedDays = int.Parse(tag);
            LoadHistoryData();
        }
    }
}
```

- [ ] **Step 3: Build to verify page compiles**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add Pages/UsageRecordPage.xaml Pages/UsageRecordPage.xaml.cs
git commit -m "feat: add UsageRecordPage for device usage tracking"
```

---

## Task 6: Update DeviceViewModel with Tracking Property

**Files:**
- Modify: `ViewModels/MainViewModel.cs`

- [ ] **Step 1: Add IsTracking property to DeviceViewModel**

In `ViewModels/MainViewModel.cs`, add the following property to the `DeviceViewModel` class (after the `TotalUsageSeconds` property, around line 28):

```csharp
    private bool _isTracking;

    public bool IsTracking
    {
        get => _isTracking;
        set
        {
            if (_isTracking != value)
            {
                _isTracking = value;
                OnPropertyChanged();
            }
        }
    }
```

- [ ] **Step 2: Initialize IsTracking in Refresh method**

In the `Refresh` method of `MainViewModel` class, add the `IsTracking` initialization in the foreach loop (after `TotalUsageSeconds = d.TotalUsageSeconds`):

```csharp
                IsTracking = Services.UsageTrackingService.Instance.IsTracking(d.DeviceId)
```

The updated `Refresh` method should have:
```csharp
            AllDevices.Add(new DeviceViewModel
            {
                DeviceId = d.DeviceId,
                Name = d.Name,
                Manufacturer = d.Manufacturer,
                Type = d.Type,
                IsEnabled = d.IsEnabled,
                IsExternal = d.IsExternal,
                VendorId = d.VendorId,
                ProductId = d.ProductId,
                UserNote = d.UserNote,
                TotalUsageSeconds = d.TotalUsageSeconds,
                IsTracking = Services.UsageTrackingService.Instance.IsTracking(d.DeviceId)
            });
```

- [ ] **Step 3: Build to verify changes compile**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 4: Commit**

```bash
git add ViewModels/MainViewModel.cs
git commit -m "feat: add IsTracking property to DeviceViewModel"
```

---

## Task 7: Update MainWindow Navigation

**Files:**
- Modify: `MainWindow.xaml`
- Modify: `MainWindow.xaml.cs`

- [ ] **Step 1: Add new navigation item in MainWindow.xaml**

In `MainWindow.xaml`, find the `<ListView>` inside the sidebar (around line 80). Add a new `ListViewItem` after `NavItemDevices` (before `NavItemStatistics`):

```xml
                    <ListViewItem x:Name="NavItemUsageRecord" Tag="UsageRecord" Margin="0,2">
                        <StackPanel Orientation="Horizontal" Spacing="12">
                            <FontIcon x:Name="IconUsageRecord" Glyph="&#xE9D9;" FontSize="16"
                                     Foreground="{StaticResource TextSecondaryBrush}"/>
                            <TextBlock x:Name="TextUsageRecord" VerticalAlignment="Center"
                                      Foreground="{StaticResource TextPrimaryBrush}"/>
                        </StackPanel>
                    </ListViewItem>
```

- [ ] **Step 2: Add navigation case in MainWindow.xaml.cs**

In `MainWindow.xaml.cs`, add the following case to the `NavList_SelectionChanged` switch statement (after the `"Devices"` case, before the `"Statistics"` case):

```csharp
            case "UsageRecord":
                IconUsageRecord.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(UsageRecordPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderUsageRecord"];
                HeaderSubtitle.Text = _loc["HeaderUsageRecordDesc"];
                break;
```

- [ ] **Step 3: Add UpdateTexts entry for new navigation item**

In `MainWindow.xaml.cs`, add the following line to the `UpdateTexts` method:

```csharp
        TextUsageRecord.Text = _loc["NavUsageRecord"];
```

- [ ] **Step 4: Add icon reset for new item**

In `MainWindow.xaml.cs`, in the `NavList_SelectionChanged` method, add this line after the other icon resets (around line 81):

```csharp
        IconUsageRecord.Foreground = secondaryBrush;
```

- [ ] **Step 5: Initialize UsageTrackingService in MainWindow constructor**

In `MainWindow.xaml.cs`, add this line in the constructor after `_deviceManager = _viewModel.GetDeviceManager();` (around line 33):

```csharp
        // Initialize usage tracking service
        Services.UsageTrackingService.Instance.Initialize(_deviceManager);
```

- [ ] **Step 6: Dispose UsageTrackingService on window close**

In `MainWindow.xaml.cs`, add this line to the `OnClosed` method (after `_deviceManager?.Dispose()`):

```csharp
        Services.UsageTrackingService.Instance.Dispose();
```

- [ ] **Step 7: Build to verify navigation changes compile**

Run: `dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 8: Commit**

```bash
git add MainWindow.xaml MainWindow.xaml.cs
git commit -m "feat: add UsageRecord page to navigation"
```

---

## Task 8: Final Build and Test

**Files:**
- None (build and test only)

- [ ] **Step 1: Clean and rebuild**

Run: `dotnet clean && dotnet build -c Debug -p:Platform=x64`
Expected: Build succeeded with no errors

- [ ] **Step 2: Run application**

Run: `dotnet run -c Debug -p:Platform=x64`
Expected: Application launches, shows new "Usage Record" navigation item

- [ ] **Step 3: Manual test checklist**

1. [ ] Application launches successfully
2. [ ] "Usage Record" appears in navigation between "Devices" and "Statistics"
3. [ ] Usage Record page shows device list with toggle switches
4. [ ] Toggling a device's tracking updates the UI
5. [ ] Today's usage stats update when tracking is enabled
6. [ ] Retention days setting works
7. [ ] History chart displays for tracked devices
8. [ ] Data persists after app restart

- [ ] **Step 4: Final commit if all tests pass**

```bash
git add -A
git commit -m "feat: complete device usage tracking feature"
```

---

## Summary

This implementation adds:

1. **Data Models** (`Models/DeviceUsageRecord.cs`, `Models/TrackingSettings.cs`)
   - Storage structures for usage records and tracking settings

2. **UsageTrackingService** (`Services/UsageTrackingService.cs`)
   - Core service managing tracking logic
   - Integrates with DeviceManager for active input detection
   - Timer-based enabled time tracking
   - Automatic data persistence and cleanup

3. **UsageRecordPage** (`Pages/UsageRecordPage.xaml`, `Pages/UsageRecordPage.xaml.cs`)
   - Device list with tracking toggles
   - Today's usage statistics
   - Retention days configuration
   - Usage history chart and data list

4. **Localization** (modified `Services/LocalizationService.cs`)
   - 20+ new localized strings for English and Chinese

5. **Navigation Integration** (modified `MainWindow.xaml`, `MainWindow.xaml.cs`)
   - New navigation item for Usage Record page
   - Service initialization and cleanup