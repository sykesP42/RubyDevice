using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RubyDevice.Models;
using RubyDevice.ViewModels;

namespace RubyDevice.Services;

/// <summary>
/// Service for exporting usage data to various formats
/// </summary>
public static class DataExportService
{
    /// <summary>
    /// Generate CSV content from usage records
    /// </summary>
    /// <param name="records">List of usage records</param>
    /// <param name="devices">List of devices for name lookup</param>
    /// <returns>CSV formatted string</returns>
    public static string GenerateCsvContent(List<DeviceUsageRecord> records, List<DeviceViewModel> devices)
    {
        if (records.Count == 0)
            return string.Empty;

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Date,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Active Time (formatted),Enabled Time (formatted)");

        // Data rows
        foreach (var record in records.OrderByDescending(r => r.Date))
        {
            var deviceName = deviceLookup.TryGetValue(record.DeviceId, out var name) ? name : record.DeviceId;
            var activeFormatted = FormatTime(record.ActiveSeconds);
            var enabledFormatted = FormatTime(record.EnabledSeconds);

            sb.AppendLine($"{record.Date:yyyy-MM-dd},{EscapeCsvField(deviceName)},{EscapeCsvField(record.DeviceId)},{record.ActiveSeconds},{record.EnabledSeconds},{activeFormatted},{enabledFormatted}");
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

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var sb = new StringBuilder();

        sb.AppendLine("Week Start,Week End,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data");

        foreach (var summary in summaries.OrderByDescending(w => w.WeekStartDate))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            sb.AppendLine($"{summary.WeekStartDate:yyyy-MM-dd},{summary.WeekEndDate:yyyy-MM-dd},{EscapeCsvField(deviceName)},{EscapeCsvField(summary.DeviceId)},{summary.TotalActiveSeconds},{summary.TotalEnabledSeconds},{summary.DaysWithData}");
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

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var sb = new StringBuilder();

        sb.AppendLine("Year,Month,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data");

        foreach (var summary in summaries.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            sb.AppendLine($"{summary.Year},{summary.Month},{EscapeCsvField(deviceName)},{EscapeCsvField(summary.DeviceId)},{summary.TotalActiveSeconds},{summary.TotalEnabledSeconds},{summary.DaysWithData}");
        }

        return sb.ToString();
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
    /// Format seconds to human-readable time
    /// </summary>
    private static string FormatTime(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        var secs = totalSeconds % 60;

        if (hours > 0)
            return $"{hours}h {mins}m";
        else if (mins > 0)
            return $"{mins}m {secs}s";
        else
            return $"{secs}s";
    }
}