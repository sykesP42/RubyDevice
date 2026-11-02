using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RubyDevice.Models;
using RubyDevice.ViewModels;

namespace RubyDevice.Services;

/// <summary>
/// Service for exporting usage data to CSV format.
/// Supports daily records, weekly summaries, and monthly summaries.
/// </summary>
public static class DataExportService
{
    /// <summary>
    /// Generate CSV content from daily usage records
    /// </summary>
    /// <param name="records">List of usage records</param>
    /// <param name="devices">List of devices for name lookup</param>
    /// <returns>CSV formatted string</returns>
    public static string GenerateCsvContent(List<DeviceUsageRecord> records, List<DeviceViewModel> devices)
    {
        if (records.Count == 0)
            return string.Empty;

        var deviceLookup = BuildDeviceLookup(devices);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Date,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Active Time (formatted),Enabled Time (formatted)");

        // Data rows
        foreach (var record in records.OrderByDescending(r => r.Date))
        {
            var deviceName = deviceLookup.TryGetValue(record.DeviceId, out var name) ? name : record.DeviceId;
            var activeFormatted = FormatTimeWithSeconds(record.ActiveSeconds);
            var enabledFormatted = FormatTimeWithSeconds(record.EnabledSeconds);

            sb.AppendLine(string.Join(",",
                record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EscapeCsvField(deviceName),
                EscapeCsvField(record.DeviceId),
                FormatNumber(record.ActiveSeconds),
                FormatNumber(record.EnabledSeconds),
                activeFormatted,
                enabledFormatted));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate CSV for aggregated weekly data
    /// </summary>
    public static string GenerateWeeklyCsvContent(List<WeeklyUsageSummary> summaries, List<DeviceViewModel> devices)
    {
        if (summaries.Count == 0)
            return string.Empty;

        var deviceLookup = BuildDeviceLookup(devices);
        var sb = new StringBuilder();

        sb.AppendLine("Week Start,Week End,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data");

        foreach (var summary in summaries.OrderByDescending(w => w.WeekStartDate))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            sb.AppendLine(string.Join(",",
                summary.WeekStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                summary.WeekEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EscapeCsvField(deviceName),
                EscapeCsvField(summary.DeviceId),
                FormatNumber(summary.TotalActiveSeconds),
                FormatNumber(summary.TotalEnabledSeconds),
                summary.DaysWithData.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate CSV for aggregated monthly data
    /// </summary>
    public static string GenerateMonthlyCsvContent(List<MonthlyUsageSummary> summaries, List<DeviceViewModel> devices)
    {
        if (summaries.Count == 0)
            return string.Empty;

        var deviceLookup = BuildDeviceLookup(devices);
        var sb = new StringBuilder();

        sb.AppendLine("Year,Month,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data");

        foreach (var summary in summaries.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            sb.AppendLine(string.Join(",",
                summary.Year.ToString(CultureInfo.InvariantCulture),
                summary.Month.ToString(CultureInfo.InvariantCulture),
                EscapeCsvField(deviceName),
                EscapeCsvField(summary.DeviceId),
                FormatNumber(summary.TotalActiveSeconds),
                FormatNumber(summary.TotalEnabledSeconds),
                summary.DaysWithData.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds a device id -> name lookup. Duplicate ids (possible with fallback
    /// entries) keep the first name instead of throwing.
    /// </summary>
    private static Dictionary<string, string> BuildDeviceLookup(List<DeviceViewModel> devices)
    {
        return devices
            .Where(d => !string.IsNullOrEmpty(d.DeviceId))
            .GroupBy(d => d.DeviceId)
            .ToDictionary(g => g.Key, g => g.First().Name);
    }

    /// <summary>
    /// Formats a numeric CSV cell with InvariantCulture so decimal commas from
    /// the user's locale never shift the columns.
    /// </summary>
    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Escape a field for CSV format (handle commas, quotes, newlines)
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    /// Format seconds to human-readable time including a seconds component
    /// </summary>
    private static string FormatTimeWithSeconds(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        var secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}h {mins}m";
        if (mins > 0)
            return $"{mins}m {secs}s";
        return $"{secs}s";
    }
}
