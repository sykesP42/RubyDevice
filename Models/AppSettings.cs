using System;
using System.IO;
using System.Text.Json;

namespace RubyDevice.Models;

/// <summary>
/// Persisted application settings stored in %AppData%\RubyDevice\app_settings.json.
/// Settings are loaded at startup and saved on each change.
/// </summary>
public class AppSettings
{
    /// <summary>Launch RubyDevice automatically when the user logs into Windows.</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>Refresh the device list automatically when devices are plugged or unplugged.</summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>Show in-app notifications for device state changes.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Interface language. One of "English" or "Chinese".</summary>
    public string Language { get; set; } = "English";

    /// <summary>Active theme name. One of "Light", "Dark", "Ocean", "Forest", "Sunset".</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>Hide to the notification area when the window is closed instead of exiting.</summary>
    public bool CloseToTray { get; set; } = false;

    /// <summary>Saved normal (restored) window width in pixels; 0 means "use default".</summary>
    public int WindowWidth { get; set; } = 0;

    /// <summary>Saved normal (restored) window height in pixels; 0 means "use default".</summary>
    public int WindowHeight { get; set; } = 0;

    /// <summary>Saved window X position in pixels; int.MinValue means "let the OS place it".</summary>
    public int WindowX { get; set; } = int.MinValue;

    /// <summary>Saved window Y position in pixels; int.MinValue means "let the OS place it".</summary>
    public int WindowY { get; set; } = int.MinValue;

    /// <summary>Whether the window was maximized at last close.</summary>
    public bool WindowMaximized { get; set; } = false;

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
}
