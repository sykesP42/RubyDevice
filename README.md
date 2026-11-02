# RubyDevice

**RubyDevice** is a modern Windows input device manager built on WinUI 3. It lets you view, manage, and control every input device (keyboard, mouse, touchpad) connected to your computer.

## Features

- **Device enumeration** - Automatically detects and lists all HID input devices, with details (manufacturer, VID/PID, connection type)
- **Device control** - Enable or disable specific input devices with one click, no administrator rights required
- **Safety confirmations** - Two-stage confirmation flow: a 5-second confirm countdown, then a 10-second auto-restore countdown after disabling
- **Emergency unlock** - Hold **both Shift keys together** to instantly re-enable every device if blocking ever misfires
- **Usage statistics** - Tracks active and enabled time per device with daily/weekly/monthly charts
- **In-app notifications** - Banner notifications for device state changes (toast notifications are unreliable for unpackaged apps)
- **Multi-language** - Built-in English and Chinese UI
- **Multi-theme** - 5 theme palettes (Light, Dark, Ocean, Forest, Sunset)
- **Hotplug auto-refresh** - Device list refreshes automatically when devices are plugged or unplugged

## Navigation

The left sidebar has six pages:

- **Devices** - Device list with single or batch enable/disable, and device details
- **Usage Record** - Per-day usage per device with charts and CSV export
- **Statistics** - Device counts, type distribution, and usage overview
- **Timer** - Schedule an auto-restore countdown for all disabled devices (keeps running when you leave the page)
- **Settings** - Language, theme, auto-start with Windows, auto-refresh, notifications, data management
- **About** - Version and license info

## System Requirements

- Windows 10 version 1809 (17763) or higher
- .NET 9.0 SDK
- Visual Studio 2022 (any edition) or VS Build Tools - the PRI resource tasks ship with VS; `Directory.Build.props` probes the standard install paths automatically

## Building

```bash
dotnet build -c Debug -p:Platform=x64
dotnet run -c Debug -p:Platform=x64
```

## Running Tests

```bash
dotnet test tests/RubyDevice.Tests -p:Platform=x64
```

The test project references the app project directly and covers the real `TimeHelper`, `DataExportService` (CSV formats, escaping, culture-invariant numbers), and usage model display properties.

## Data Storage

All configuration and statistics are saved in `%AppData%\RubyDevice\`:

| File | Content |
| --- | --- |
| `device_data.json` | Device notes cache |
| `usage_data.json` | Daily usage records (active / enabled seconds) |
| `tracking_settings.json` | Per-device tracking toggles and retention days |
| `timer_settings.json` | Last auto-restore timeout setting |
| `app_settings.json` | Language, theme, behavior toggles |
| `crash.log` | Unhandled exception log (created on first crash) |

Devices are identified by a **stable ID derived from their device path** (VID/PID or ACPI id), so notes, tracking settings, and history survive reboots. Delete these files to reset the app to its initial state.

## CSV Export

The "Usage Record" page exports the selected range as CSV with date, device name, device ID, active and enabled time. Numbers use `InvariantCulture` so the columns never shift on comma-decimal locales, and the file is written as UTF-8 with BOM so Excel renders non-ASCII device names correctly.

## Project Structure

```
RubyDevice/
├── App.xaml.cs          Entry point, theme switching, crash log
├── MainWindow.xaml.cs   Main window, navigation, raw input registration, in-app notifications
├── Core/                Device enumeration and input blocking (Raw Input API + low-level hooks)
├── Models/              Data models and persistence structures
├── Services/            Localization, theme, notification, usage tracking, device watcher, export, auto-start
├── ViewModels/          MVVM view models (device state + app-wide auto-restore countdown)
├── Pages/               Feature pages
├── Controls/            Confirm and auto-restore dialogs
├── Helpers/             Shared utilities
├── Themes/              5 theme resource dictionaries
├── docs/                Design documents
└── tests/               Unit tests
```

## Tech Stack

- WinUI 3 (Windows App SDK 1.5)
- .NET 9.0
- C# / XAML
- MVVM architecture

## License

MIT License
