using System;
using System.IO;
using System.Text.Json;

namespace RubyDevice.Models;

/// <summary>
/// Persisted application settings stored in %AppData%\RubyDevice\app_settings.json.
/// Settings are loaded at startup and saved on each toggle change.
/// </summary>
public class AppSettings
{
    /// <summary>Launch RubyDevice automatically when the user logs into Windows.</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>Periodically refresh the device list in the background.</summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>Show toast notifications for device state changes.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Minimize to system tray instead of the taskbar when the minimize button is clicked.</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Show the active device count on the system tray icon tooltip.</summary>
    public bool ShowDeviceCount { get; set; } = true;

    /// <summary>Minimize to tray instead of closing when the window close button is clicked.</summary>
    public bool CloseToTray { get; set; } = false;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RubyDevice", "app_settings.json");

    /// <summary>
    /// Load settings from file
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    /// <summary>
    /// Save settings to file
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>
    /// Update a single setting value and save
    /// </summary>
    public void Set<T>(string key, T value)
    {
        typeof(AppSettings).GetProperty(key)?.SetValue(this, value);
        Save();
    }
}
