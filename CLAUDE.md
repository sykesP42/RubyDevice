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

PRI generation needs the Appx packaging tasks shipped with Visual Studio 2022 / Build Tools. `Directory.Build.props` probes the standard per-edition install paths; if none exists the build fails with an MSB4062 from `MrtCore.PriGen.targets`.

## Tests

```bash
dotnet test tests/RubyDevice.Tests -p:Platform=x64
```

The test project targets `net9.0-windows10.0.19041.0` and has a direct `ProjectReference` to the app, so it tests the **real** `TimeHelper`, `DataExportService` (CSV format, escaping, culture-invariant number formatting), and usage model display properties. If you change any of those, the tests fail rather than silently passing on stale copies.

## Architecture Overview

RubyDevice is a WinUI 3 desktop application for managing Windows input devices (keyboards, mice, touchpads). It uses MVVM architecture with the following structure:

### Core Layer (`Core/`)
- **DeviceManager.cs** - Central device management using Windows Raw Input API
  - Enumerates HID devices via `GetRawInputDeviceList`
  - **DeviceId is a stable identity derived from the device path** (VID/PID or ACPI id), not the raw input handle, so cached notes and usage history survive reboots
  - Groups composite device interfaces by that identity (touchpads expose multiple interfaces)
  - Blocks disabled devices via low-level hooks; tracks the **last raw input device per type** (keyboard and mouse independently) so blocking a keyboard can never freeze mice and vice versa
  - **Emergency unlock**: holding both Shift keys clears all blocking from inside the hook (blocked input never generates WM_INPUT, so the last-known device can go stale)
  - `Devices` is guarded by `_stateLock`; use `GetDevicesSnapshot()` when iterating from other threads
  - `ProcessRawInput` decodes only the `RAWINPUTHEADER` (marshaling the full `RAWINPUT` struct would read past the buffer returned by `GetRawInputData`)
  - The hook delegate is rooted in `_hookProc` for the process lifetime; if it were collected, native input would call freed memory
- **WindowSubclassHelper.cs** - comctl32 window subclass to receive WM_INPUT. The subclass proc is held in a static field so the delegate stays alive; only one subclass can be installed at a time

### Services (`Services/`)
- **LocalizationService** - Singleton managing English/Chinese strings with `INotifyPropertyChanged`
- **ThemeService** + `App.ChangeTheme` - 5 palettes (Light, Dark, Ocean, Forest, Sunset). `ChangeTheme` swaps the merged dictionary and then forces `RequestedTheme` (Light for all palettes except Dark) so default control colors match; the flip also re-evaluates `{ThemeResource}` references, which is why pages use `{ThemeResource}` and not `{StaticResource}`
- **UsageTrackingService** - Active time accumulates from `DeviceManager.DeviceActivity` events, crediting only gaps of 2 seconds or less (longer gaps count as idle). Enabled time is credited with real elapsed time between ticks (capped per tick to survive suspension). `CleanupOldData` runs at startup and daily. Persists to `%AppData%\RubyDevice\`
- **NotificationService** - Raises `NotificationPosted`; MainWindow renders it as an in-app InfoBar. System toasts are not used because unpackaged apps lack the required AUMID shortcut
- **DeviceWatcherService** - WMI `Win32_DeviceChangeEvent` with a 1s debounce (guarded by a lock; raised from a thread-pool thread)
- **AutoStartHelper** - HKCU `Run` registry key for start-with-Windows
- **TrayIconService** - Shell_NotifyIcon tray icon owned by a hidden message-only window (class `RubyDeviceTrayWnd`). Left click/`Open` raises `OpenRequested`, the menu's `Exit` raises `ExitRequested`. Create/use/dispose on the UI thread; the WndProc delegate is rooted in a field

### ViewModels (`ViewModels/`)
- **MainViewModel** - Main application state, device list, navigation, and the **app-wide auto-restore countdown** (DispatcherTimer + deadline-based, lives here so it survives page navigation)
- **DeviceViewModel** - Per-device UI state; `ApplyFrom` re-syncs an existing instance in place so pages holding references stay valid
- Device identity used across tracking/export/UI is the stable `DeviceId` from DeviceManager

### Models (`Models/`)
- **DeviceUsageRecord** - Daily usage data (ActiveSeconds, EnabledSeconds as double)
- **AppSettings** - Persists language, theme, AutoStart, AutoRefresh, ShowNotifications to `app_settings.json`

### Pages (`Pages/`)
Navigation-based UI with Frame navigation from MainWindow. Each page receives `MainViewModel` as navigation parameter. MainWindow clears the Frame back stack on top-level navigation so device-detail navigation never leaks across sections.

### Design system
- Each theme dictionary also defines tint brushes (`PrimaryTintBrush`,
  `SuccessTintBrush`, `WarningTintBrush`, `DangerTintBrush`) used by status
  pills and dialog icon tiles, plus `ListViewItemBackgroundSelected` /
  `ListViewItemBackgroundPointerOver` for the Win11-style navigation pill
- **Popups do not inherit the window's forced RequestedTheme** - every
  ContentDialog must set `RequestedTheme = ThemeService.Instance.FluentTheme`
  or it renders with the system theme and its text goes unreadable
- Live input feedback: device rows show a breathing opacity dot
  (DevicesPage `DeviceRow_Loaded` storyboard) while the device is active

### Window shell
- Close-to-tray: `AppWindow.Closing` is canceled and the window hidden when `AppSettings.CloseToTray` is on; the tray menu's `Exit` sets a bypass flag before calling `Close()`
- Window geometry: normal bounds + maximized state are saved via `GetWindowPlacement` on close and restored via `AppWindow.Resize`/`Move` at launch; off-screen positions are rejected by a virtual-screen check. Both APIs live in the same DPI-scaled coordinate space, so save/restore is symmetric

## Key Implementation Details

### Device Enumeration
Devices are grouped by physical device identity:
1. VID+PID for USB devices
2. ACPI path prefix for built-in devices
3. Special handling for "UNIW0001" and "Microsoft HID RID" (internal touchpad)

### Device Blocking
Uses `SetWindowsHookEx` with `WH_KEYBOARD_LL` / `WH_MOUSE_LL`. When a device is disabled:
- All handles for that physical device are added to `_blockedHandles`
- The hook procedure compares against the last raw input device **of the same type** and swallows matching input
- Disabling restores correctly across `RefreshDevices()` via `_disabledDeviceIds`
- `ToggleDevice(disable)` fails (returns false) when hook installation fails, instead of pretending the device is blocked

### Activity Tracking
`DeviceManager.DeviceActivity` fires when input is detected (throttled to 100ms per device). UI highlights the active device via `ActiveDeviceId` and the row's `ActiveBrush` binding.

### Data Persistence
- Device notes cache: `%AppData%\RubyDevice\device_data.json`
- Usage records: `%AppData%\RubyDevice\usage_data.json`
- Tracking settings: `%AppData%\RubyDevice\tracking_settings.json`
- App settings: `%AppData%\RubyDevice\app_settings.json`

## Common Patterns

### Adding a new localized string
1. Add key to `EnglishStrings` dictionary in `LocalizationService.cs`
2. Add corresponding key to `ChineseStrings` dictionary
3. Reference via `LocalizationService.Instance["Key"]`

### Adding a new page
1. Create XAML + code-behind in `Pages/`
2. Add navigation item in `MainWindow.xaml`
3. Add case in `MainWindow.xaml.cs` `NavigateToPage`
4. Update `UpdateTexts()` method for the new nav item

### Thread Safety
- UI updates must use `DispatcherQueue.TryEnqueue()`; MainViewModel captures the UI `DispatcherQueue` in its constructor for the watcher callback
- Services use `lock` for thread-safe access to shared state
- Pages should check `_isLoaded` flag before UI operations
