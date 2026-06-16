# Device Usage Tracking Feature Design

**Date:** 2026-06-16
**Status:** Approved

## Overview

Add an optional device usage tracking feature that records both "enabled time" and "active time" for selected devices on a daily basis. Users can manually enable tracking for each device, view historical usage trends, and configure automatic data cleanup.

## Goals

- Track device usage time (enabled duration + active duration)
- Per-device tracking control (opt-in, default off)
- Daily aggregation for trend analysis
- Automatic data cleanup to prevent unbounded growth

## Non-Goals

- Session-level detailed tracking (start/stop timestamps)
- Real-time usage display during tracking
- Cross-device usage correlation analysis

## Architecture

### New Components

1. **UsageTrackingService** - Core service managing tracking logic
2. **UsageRecordPage** - New navigation page for tracking management
3. **Data models** - Storage structures for usage records and settings

### Integration Points

- `DeviceManager.ProcessRawInput()` - Hook for active input detection
- `MainWindow` navigation - Insert new page before Statistics
- `LocalizationService` - Add new localized strings

## Data Models

### DeviceUsageRecord

Single day's usage record for one device.

```
DeviceId: string          - Unique device identifier
Date: DateTime            - Date only (no time component)
ActiveSeconds: long       - Cumulative active time for the day
EnabledSeconds: long      - Cumulative enabled time for the day
```

### TrackingSettings

Per-device tracking configuration.

```
DeviceId: string          - Unique device identifier
IsTrackingEnabled: bool   - Whether tracking is enabled for this device
FirstTrackedDate: DateTime? - Date when tracking was first enabled (nullable)
```

### AppTrackingConfig

Global tracking configuration.

```
RetentionDays: int        - Days to keep historical data (default: 30)
AutoCleanup: bool         - Whether to auto-delete old records (default: true)
```

## UsageTrackingService Design

### Responsibilities

1. Manage tracking settings per device
2. Track active time via Raw Input events
3. Track enabled time via periodic timer (every minute)
4. Aggregate and persist daily usage records
5. Perform automatic cleanup of expired data

### Key Methods

| Method | Description |
|--------|-------------|
| `Initialize()` | Load settings, restore tracking state on app start |
| `SetTracking(deviceId, enabled)` | Enable/disable tracking for a device |
| `IsTracking(deviceId)` | Check if a device is being tracked |
| `ProcessActiveInput(deviceHandle)` | Called by DeviceManager on input event |
| `GetUsageHistory(deviceId, days)` | Retrieve historical records for a device |
| `GetAllTrackingDevices()` | List all devices with tracking enabled |
| `SaveData()` | Persist current data to disk |
| `CleanupOldData()` | Delete records older than retention period |

### Internal Logic

**Active Time Tracking:**
- When `ProcessRawInput()` identifies a device handle
- If that device has tracking enabled
- Increment `ActiveSeconds` for current day's record

**Enabled Time Tracking:**
- Timer runs every 60 seconds
- For each tracked device that is currently enabled
- Increment `EnabledSeconds` by 60 for current day's record

**Data Persistence:**
- Save to disk every 5 minutes (or on app close)
- File: `%AppData%\RubyDevice\usage_data.json`
- Settings: `%AppData%\RubyDevice\tracking_settings.json`

## UsageRecordPage Design

### Page Sections

**1. Device Tracking Controls (Top)**
- List all devices as cards
- Each card: device name, type icon, ToggleSwitch for tracking
- Visual indication: green when enabled, gray when disabled

**2. Statistics Overview (Middle)**
- Number of devices currently tracked
- Today's total active time / enabled time
- Retention days setting (NumberBox with range 7-365)

**3. History Details (Bottom)**
- Device selector dropdown
- Time range selector: 7 days / 30 days / All
- Bar chart showing daily active time
- Data table: date, active seconds, enabled seconds (descending by date)

### User Interactions

- Toggle tracking for any device
- Adjust retention days
- Select device to view history
- Change time range for chart

## Navigation Integration

### Changes to MainWindow

- Add new navigation item before "Statistics"
- Order: Devices → **Usage Record** → Statistics → Timer → Settings → About
- Icon: chart or document symbol

### Localization Additions

| Key | English | Chinese |
|-----|---------|---------|
| NavUsageRecord | Usage Record | 使用记录 |
| HeaderUsageRecord | Usage Record | 使用记录 |
| HeaderUsageRecordDesc | Track device usage time | 追踪设备使用时间 |
| TrackingEnabled | Tracking Enabled | 已开启追踪 |
| TrackingDisabled | Tracking Disabled | 未开启追踪 |
| TrackedDevices | Tracked Devices | 追踪中的设备 |
| TodayUsage | Today's Usage | 今日使用 |
| ActiveTime | Active Time | 活跃时长 |
| EnabledTime | Enabled Time | 启用时长 |
| RetentionDays | Data Retention Days | 数据保留天数 |
| AutoCleanup | Auto Cleanup | 自动清理 |
| NoTrackingDevices | No devices are being tracked | 暂无追踪中的设备 |
| EnableTrackingHint | Enable tracking on a device to start recording | 开启设备追踪以开始记录 |
| Last7Days | Last 7 Days | 最近 7 天 |
| Last30Days | Last 30 Days | 最近 30 天 |
| AllTime | All Time | 全部时间 |
| SelectDevice | Select Device | 选择设备 |
| UsageHistory | Usage History | 使用历史 |
| Date | Date | 日期 |
| TrackedSince | Tracked since {0} | 自 {0} 开始追踪 |

## File Storage

### Location

All data stored in `%AppData%\RubyDevice\`:

```
RubyDevice/
├── device_data.json       (existing - device notes/cache)
├── tracking_settings.json  (new - tracking settings + config)
└── usage_data.json         (new - daily usage records)
```

### File Formats

**tracking_settings.json:**
```json
{
  "config": {
    "retentionDays": 30,
    "autoCleanup": true
  },
  "devices": {
    "device_id_1": {
      "isTrackingEnabled": true,
      "firstTrackedDate": "2026-06-16"
    },
    "device_id_2": {
      "isTrackingEnabled": false,
      "firstTrackedDate": null
    }
  }
}
```

**usage_data.json:**
```json
{
  "records": [
    {
      "deviceId": "device_id_1",
      "date": "2026-06-16",
      "activeSeconds": 3600,
      "enabledSeconds": 7200
    }
  ]
}
```

## Error Handling

- File read/write failures: Log silently, continue with empty/default data
- Device disconnect during tracking: Stop tracking for that device until reconnected
- Invalid data format: Reset to defaults, log warning

## Implementation Notes

### DeviceManager Integration

Modify `ProcessRawInput()` to call `UsageTrackingService.ProcessActiveInput()`:

```csharp
public void ProcessRawInput(IntPtr hRawInput)
{
    // ... existing logic to identify device handle ...

    // Notify tracking service
    UsageTrackingService.Instance.ProcessActiveInput(raw.header.hDevice);
}
```

### Timer Management

- Create timer in `UsageTrackingService.Initialize()`
- Timer interval: 60 seconds
- Timer action: Update enabled time for all tracked, enabled devices
- Dispose timer in `MainWindow.OnClosed()` via service shutdown

### Chart Implementation

Use WinUI 3 built-in controls or simple custom bar chart:
- Canvas with colored rectangles proportional to values
- Labels for dates and values
- No external chart library required (keep dependencies minimal)