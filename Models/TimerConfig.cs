using System;
using System.IO;
using System.Text.Json;

namespace RubyDevice.Models;

/// <summary>
/// Persisted timer settings stored in %AppData%\RubyDevice\timer_settings.json.
/// Saves the last used timeout value across app restarts.
/// </summary>
public class TimerConfig
{
    /// <summary>Auto-restore timeout in minutes (1-480).</summary>
    public int TimeoutMinutes { get; set; } = 30;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RubyDevice", "timer_settings.json");

    /// <summary>Load timer settings from disk, or return defaults if file doesn't exist.</summary>
    public static TimerConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<TimerConfig>(json) ?? new TimerConfig();
            }
        }
        catch { }
        return new TimerConfig();
    }

    /// <summary>Save timer settings to disk.</summary>
    /// <remarks>
    /// Persistence is best-effort: IO and serialization failures are swallowed so
    /// that an unwritable or locked AppData directory can never crash the app.
    /// A failed save therefore leaves the previous file contents untouched.
    /// </remarks>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
