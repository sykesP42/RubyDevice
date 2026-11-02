using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;
using Xunit;

namespace RubyDevice.Tests;

/// <summary>
/// Tests for the real DataExportService, exercising the CSV formats, escaping,
/// device-name lookup, and culture-invariant number formatting.
/// </summary>
public class DataExportServiceTests
{
    private static DeviceViewModel Device(string id, string name) =>
        new() { DeviceId = id, Name = name };

    private static DeviceUsageRecord Record(string deviceId, DateTime date, double active, double enabled) =>
        new() { DeviceId = deviceId, Date = date, ActiveSeconds = active, EnabledSeconds = enabled };

    #region CSV Generation Tests

    [Fact]
    public void GenerateCsvContent_WithEmptyRecords_ReturnsEmptyString()
    {
        var result = DataExportService.GenerateCsvContent(new List<DeviceUsageRecord>(), new List<DeviceViewModel>());
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateCsvContent_WithValidRecords_ReturnsCsvWithHeader()
    {
        var records = new List<DeviceUsageRecord>
        {
            Record("test-device-1", new DateTime(2026, 7, 1), 3600, 7200)
        };

        var result = DataExportService.GenerateCsvContent(records, new List<DeviceViewModel>());

        Assert.StartsWith("Date,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Active Time (formatted),Enabled Time (formatted)", result);
        Assert.Contains("2026-07-01", result);
        Assert.Contains("test-device-1", result);
        Assert.Contains(",3600,", result);
        Assert.Contains(",7200,", result);
    }

    [Fact]
    public void GenerateCsvContent_WithDeviceNameLookup_UsesDeviceName()
    {
        var records = new List<DeviceUsageRecord>
        {
            Record("device-1", new DateTime(2026, 7, 1), 1800, 3600)
        };
        var devices = new List<DeviceViewModel> { Device("device-1", "Test Keyboard") };

        var result = DataExportService.GenerateCsvContent(records, devices);

        Assert.Contains("Test Keyboard", result);
    }

    [Fact]
    public void GenerateCsvContent_WithUnknownDevice_FallsBackToDeviceId()
    {
        var records = new List<DeviceUsageRecord>
        {
            Record("orphan-device", new DateTime(2026, 7, 1), 10, 20)
        };

        var result = DataExportService.GenerateCsvContent(records, new List<DeviceViewModel>());

        Assert.Contains("orphan-device", result);
    }

    [Fact]
    public void GenerateCsvContent_WithDuplicateDeviceIds_DoesNotThrow()
    {
        // Fallback devices can share an empty id; the lookup must keep the first
        var devices = new List<DeviceViewModel>
        {
            Device("", "Keyboard"),
            Device("", "Mouse")
        };
        var records = new List<DeviceUsageRecord>
        {
            Record("", new DateTime(2026, 7, 1), 10, 20)
        };

        var result = DataExportService.GenerateCsvContent(records, devices);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void GenerateCsvContent_WithFractionalSeconds_UsesInvariantDecimalPoint()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            // Comma-decimal locale must not shift the CSV columns
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var records = new List<DeviceUsageRecord>
            {
                Record("device-1", new DateTime(2026, 7, 1), 3600.5, 7200.25)
            };

            var result = DataExportService.GenerateCsvContent(records, new List<DeviceViewModel>());

            Assert.Contains(",3600.5,", result);
            Assert.Contains(",7200.25,", result);
            Assert.DoesNotContain("3600,5", result);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    #endregion

    #region CSV Field Escaping Tests

    public static TheoryData<string, string> EscapingCases => new()
    {
        { "Normal Device Name", "Normal Device Name" },
        { "Device, Name", "\"Device, Name\"" },
        { "Device \"Name\"", "\"Device \"\"Name\"\"\"" },
        { "Line1\nLine2", "\"Line1\nLine2\"" },
    };

    [Theory]
    [MemberData(nameof(EscapingCases))]
    public void GenerateCsvContent_EscapesSpecialCharacters(string deviceName, string expectedField)
    {
        var records = new List<DeviceUsageRecord>
        {
            Record("device-1", new DateTime(2026, 7, 1), 10, 20)
        };
        var devices = new List<DeviceViewModel> { Device("device-1", deviceName) };

        var result = DataExportService.GenerateCsvContent(records, devices);

        Assert.Contains(expectedField, result);
    }

    [Fact]
    public void GenerateCsvContent_WithEmptyDeviceName_RendersEmptyField()
    {
        var records = new List<DeviceUsageRecord>
        {
            Record("device-1", new DateTime(2026, 7, 1), 10, 20)
        };
        var devices = new List<DeviceViewModel> { Device("device-1", "") };

        var result = DataExportService.GenerateCsvContent(records, devices);

        Assert.Contains(",device-1,", result);
    }

    #endregion

    #region Weekly / Monthly CSV Tests

    [Fact]
    public void GenerateWeeklyCsvContent_WithEmptySummaries_ReturnsEmptyString()
    {
        var result = DataExportService.GenerateWeeklyCsvContent(new List<WeeklyUsageSummary>(), new List<DeviceViewModel>());
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateWeeklyCsvContent_WithValidData_ReturnsCorrectFormat()
    {
        var summaries = new List<WeeklyUsageSummary>
        {
            new WeeklyUsageSummary
            {
                DeviceId = "test-device",
                WeekStartDate = new DateTime(2026, 7, 1),
                WeekEndDate = new DateTime(2026, 7, 7),
                TotalActiveSeconds = 10000,
                TotalEnabledSeconds = 20000,
                DaysWithData = 5
            }
        };

        var result = DataExportService.GenerateWeeklyCsvContent(summaries, new List<DeviceViewModel>());

        Assert.StartsWith("Week Start,Week End,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data", result);
        Assert.Contains("2026-07-01", result);
        Assert.Contains("2026-07-07", result);
        Assert.Contains(",10000,", result);
        Assert.Contains(",20000,", result);
        Assert.Contains(",5", result);
    }

    [Fact]
    public void GenerateMonthlyCsvContent_WithValidData_ReturnsCorrectFormat()
    {
        var summaries = new List<MonthlyUsageSummary>
        {
            new MonthlyUsageSummary
            {
                DeviceId = "test-device",
                Year = 2026,
                Month = 7,
                TotalActiveSeconds = 50000,
                TotalEnabledSeconds = 100000,
                DaysWithData = 20
            }
        };

        var result = DataExportService.GenerateMonthlyCsvContent(summaries, new List<DeviceViewModel>());

        Assert.StartsWith("Year,Month,Device Name,Device ID,Active Time (seconds),Enabled Time (seconds),Days with Data", result);
        Assert.Contains(",50000,", result);
        Assert.Contains(",100000,", result);
        Assert.Contains(",20", result);
    }

    #endregion
}
