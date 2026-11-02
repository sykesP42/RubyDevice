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
