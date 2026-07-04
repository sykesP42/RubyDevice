using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RubyDevice.Tests;

/// <summary>
/// Unit tests for DataExportService logic
/// Note: Tests the pure logic without WinUI dependencies
/// </summary>
public class DataExportServiceTests
{
    #region CSV Generation Tests

    [Fact]
    public void GenerateCsvContent_WithEmptyRecords_ReturnsEmptyString()
    {
        var records = new List<TestUsageRecord>();
        var devices = new List<TestDeviceInfo>();

        var result = GenerateCsvContent(records, devices);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateCsvContent_WithValidRecords_ReturnsCsvWithHeader()
    {
        var records = new List<TestUsageRecord>
        {
            new TestUsageRecord
            {
                DeviceId = "test-device-1",
                Date = new DateTime(2026, 7, 1),
                ActiveSeconds = 3600,
                EnabledSeconds = 7200
            }
        };
        var devices = new List<TestDeviceInfo>();

        var result = GenerateCsvContent(records, devices);

        Assert.StartsWith("Date,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Active Time (formatted),Enabled Time (formatted)", result);
        Assert.Contains("2026-07-01", result);
        Assert.Contains("test-device-1", result);
        Assert.Contains("3600", result);
        Assert.Contains("7200", result);
    }

    [Fact]
    public void GenerateCsvContent_WithDeviceNameLookup_UsesDeviceName()
    {
        var records = new List<TestUsageRecord>
        {
            new TestUsageRecord
            {
                DeviceId = "device-1",
                Date = new DateTime(2026, 7, 1),
                ActiveSeconds = 1800,
                EnabledSeconds = 3600
            }
        };
        var devices = new List<TestDeviceInfo>
        {
            new TestDeviceInfo { DeviceId = "device-1", Name = "Test Keyboard" }
        };

        var result = GenerateCsvContent(records, devices);

        Assert.Contains("Test Keyboard", result);
    }

    #endregion

    #region CSV Field Escaping Tests

    [Theory]
    [InlineData("Normal Device Name", "Normal Device Name")]
    [InlineData("Device, Name", "\"Device, Name\"")]
    [InlineData("Device \"Name\"", "\"Device \"\"Name\"\"\"")]
    [InlineData("Line1\nLine2", "\"Line1\nLine2\"")]
    [InlineData("", "")]
    public void EscapeCsvField_ReturnsCorrectFormat(string input, string expected)
    {
        var result = EscapeCsvField(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Weekly CSV Tests

    [Fact]
    public void GenerateWeeklyCsvContent_WithEmptySummaries_ReturnsEmptyString()
    {
        var summaries = new List<TestWeeklySummary>();
        var devices = new List<TestDeviceInfo>();

        var result = GenerateWeeklyCsvContent(summaries, devices);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateWeeklyCsvContent_WithValidData_ReturnsCorrectFormat()
    {
        var summaries = new List<TestWeeklySummary>
        {
            new TestWeeklySummary
            {
                DeviceId = "test-device",
                WeekStartDate = new DateTime(2026, 7, 1),
                WeekEndDate = new DateTime(2026, 7, 7),
                TotalActiveSeconds = 10000,
                TotalEnabledSeconds = 20000,
                DaysWithData = 5
            }
        };
        var devices = new List<TestDeviceInfo>();

        var result = GenerateWeeklyCsvContent(summaries, devices);

        Assert.StartsWith("Week Start,Week End,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data", result);
        Assert.Contains("2026-07-01", result);
        Assert.Contains("2026-07-07", result);
        Assert.Contains("10000", result);
        Assert.Contains("5", result);
    }

    #endregion

    #region Monthly CSV Tests

    [Fact]
    public void GenerateMonthlyCsvContent_WithValidData_ReturnsCorrectFormat()
    {
        var summaries = new List<TestMonthlySummary>
        {
            new TestMonthlySummary
            {
                DeviceId = "test-device",
                Year = 2026,
                Month = 7,
                TotalActiveSeconds = 50000,
                TotalEnabledSeconds = 100000,
                DaysWithData = 20
            }
        };
        var devices = new List<TestDeviceInfo>();

        var result = GenerateMonthlyCsvContent(summaries, devices);

        Assert.StartsWith("Year,Month,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data", result);
        Assert.Contains("2026", result);
        Assert.Contains("7", result);
        Assert.Contains("50000", result);
        Assert.Contains("20", result);
    }

    #endregion

    #region Test Data Models

    private class TestUsageRecord
    {
        public string DeviceId { get; set; } = "";
        public DateTime Date { get; set; }
        public double ActiveSeconds { get; set; }
        public long EnabledSeconds { get; set; }
    }

    private class TestDeviceInfo
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private class TestWeeklySummary
    {
        public string DeviceId { get; set; } = "";
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public double TotalActiveSeconds { get; set; }
        public long TotalEnabledSeconds { get; set; }
        public int DaysWithData { get; set; }
    }

    private class TestMonthlySummary
    {
        public string DeviceId { get; set; } = "";
        public int Year { get; set; }
        public int Month { get; set; }
        public double TotalActiveSeconds { get; set; }
        public long TotalEnabledSeconds { get; set; }
        public int DaysWithData { get; set; }
    }

    #endregion

    #region Helper Methods (Copied from service for testing)

    private static string GenerateCsvContent(List<TestUsageRecord> records, List<TestDeviceInfo> devices)
    {
        if (records.Count == 0)
            return string.Empty;

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var lines = new List<string>
        {
            "Date,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Active Time (formatted),Enabled Time (formatted)"
        };

        foreach (var record in records.OrderByDescending(r => r.Date))
        {
            var deviceName = deviceLookup.TryGetValue(record.DeviceId, out var name) ? name : record.DeviceId;
            var activeFormatted = FormatTime(record.ActiveSeconds);
            var enabledFormatted = FormatTime(record.EnabledSeconds);
            lines.Add($"{record.Date:yyyy-MM-dd},{EscapeCsvField(deviceName)},{EscapeCsvField(record.DeviceId)},{record.ActiveSeconds},{record.EnabledSeconds},{activeFormatted},{enabledFormatted}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GenerateWeeklyCsvContent(List<TestWeeklySummary> summaries, List<TestDeviceInfo> devices)
    {
        if (summaries.Count == 0)
            return string.Empty;

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var lines = new List<string>
        {
            "Week Start,Week End,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data"
        };

        foreach (var summary in summaries.OrderByDescending(w => w.WeekStartDate))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            lines.Add($"{summary.WeekStartDate:yyyy-MM-dd},{summary.WeekEndDate:yyyy-MM-dd},{EscapeCsvField(deviceName)},{EscapeCsvField(summary.DeviceId)},{summary.TotalActiveSeconds},{summary.TotalEnabledSeconds},{summary.DaysWithData}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GenerateMonthlyCsvContent(List<TestMonthlySummary> summaries, List<TestDeviceInfo> devices)
    {
        if (summaries.Count == 0)
            return string.Empty;

        var deviceLookup = devices.ToDictionary(d => d.DeviceId, d => d.Name);
        var lines = new List<string>
        {
            "Year,Month,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data"
        };

        foreach (var summary in summaries.OrderByDescending(m => m.Year).ThenByDescending(m => m.Month))
        {
            var deviceName = deviceLookup.TryGetValue(summary.DeviceId, out var name) ? name : summary.DeviceId;
            lines.Add($"{summary.Year},{summary.Month},{EscapeCsvField(deviceName)},{EscapeCsvField(summary.DeviceId)},{summary.TotalActiveSeconds},{summary.TotalEnabledSeconds},{summary.DaysWithData}");
        }

        return string.Join(Environment.NewLine, lines);
    }

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

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    #endregion
}