using System;
using System.IO;
using System.Text.Json;

namespace RubyDevice.Models;

/// <summary>
/// Application settings model for persistence
/// </summary>
public class AppSettings
{
    public bool AutoStart { get; set; } = false;
    public bool AutoRefresh { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowDeviceCount { get; set; } = true;
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
