namespace RubyDevice.Helpers;

/// <summary>
/// Shared utility for formatting time values
/// </summary>
public static class TimeHelper
{
    /// <summary>
    /// Format seconds to human-readable "Xh Ym" or "Ym" format
    /// </summary>
    /// <remarks>
    /// Fractional seconds are truncated rather than rounded, and the result never
    /// carries a seconds component — callers that need second-level precision
    /// should not use this helper.
    /// </remarks>
    public static string FormatTime(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }
}
