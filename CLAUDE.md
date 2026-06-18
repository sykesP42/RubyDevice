# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```bash
# Build (x64 required - WinUI 3 doesn't support AnyCPU)
dotnet build -c Debug -p:Platform=x64

# Run
dotnet run -c Debug -p:Platform=x64

# Or run the compiled executable directly
./bin/x64/Debug/net9.0-windows10.0.19041.0/RubyDevice.exe
```

## Architecture Overview

RubyDevice is a WinUI 3 desktop application for managing Windows input devices (keyboards, mice, touchpads). It uses MVVM architecture with the following structure:

### Core Layer (`Core/`)
- **DeviceManager.cs** - Central device management using Windows Raw Input API
  - Enumerates HID devices via `GetRawInputDeviceList`
  - Groups composite device interfaces by VID/PID (touchpads expose multiple interfaces)
  - Implements low-level hooks for device blocking (no admin required)
  - **Important**: `RID_DEVICE_INFO` struct uses Union layout - see existing definition for correct size (32 bytes)

### Services (`Services/`)
- **LocalizationService** - Singleton managing English/Chinese strings with `INotifyPropertyChanged`
- **ThemeService** - 5 theme variants (Light, Dark, Ocean, Forest, Sunset)
- **UsageTrackingService** - Tracks active/enabled time per device, persists to `%AppData%\RubyDevice\`
- **NotificationService** - Toast notifications for device state changes

### ViewModels (`ViewModels/`)
- **MainViewModel** - Main application state, device list, navigation
- **DeviceViewModel** - Per-device UI state with activity tracking via `WeakReference` to parent

### Models (`Models/`)
- **DeviceUsageRecord** - Daily usage data (ActiveSeconds, EnabledSeconds)
- **TrackingSettings** - Per-device tracking configuration

### Pages (`Pages/`)
Navigation-based UI with Frame navigation from MainWindow. Each page receives `MainViewModel` as navigation parameter.

## Key Implementation Details

### Device Enumeration
Devices are grouped by physical device identity:
1. VID+PID for USB devices
2. ACPI path prefix for built-in devices
3. Special handling for "UNIW0001" and "Microsoft HID RID" (internal touchpad)

### Device Blocking
Uses `SetWindowsHookEx` with `WH_KEYBOARD_LL` / `WH_MOUSE_LL`. When a device is disabled:
- All handles for that physical device are added to `_blockedHandles`
- Hook procedure checks `_currentInputDevice` against blocked set
- Returns non-zero to block input

### Activity Tracking
`DeviceManager.DeviceActivity` event fires when input is detected (throttled to 100ms per device). UI highlights active device via binding to `MainViewModel.ActiveDeviceId`.

### Data Persistence
- Device notes/cache: `%AppData%\RubyDevice\device_data.json`
- Usage records: `%AppData%\RubyDevice\usage_data.json`
- Tracking settings: `%AppData%\RubyDevice\tracking_settings.json`

## Common Patterns

### Adding a new localized string
1. Add key to `EnglishStrings` dictionary in `LocalizationService.cs`
2. Add corresponding key to `ChineseStrings` dictionary
3. Reference via `LocalizationService.Instance["Key"]`

### Adding a new page
1. Create XAML + code-behind in `Pages/`
2. Add navigation item in `MainWindow.xaml`
3. Add case in `MainWindow.xaml.cs` `NavList_SelectionChanged`
4. Update `UpdateTexts()` method for the new nav item

### Thread Safety
- UI updates must use `DispatcherQueue.TryEnqueue()`
- Services use `lock` for thread-safe access to shared state
- Pages should check `_isLoaded` flag before UI operations
