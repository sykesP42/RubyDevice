using System;
using System.Globalization;
using System.Threading;
using RubyDevice.Models;
using Xunit;

namespace RubyDevice.Tests;

/// <summary>
/// Tests for the usage record models' display properties.
/// </summary>
public class UsageModelTests
{
    [Fact]
    public void DateKey_FormatsAsIsoDate()
    {
        var record = new DeviceUsageRecord { Date = new DateTime(2026, 11, 3) };
        Assert.Equal("2026-11-03", record.DateKey);
    }

    [Fact]
    public void WeekLabel_RendersStartMonthAndEndDay()
    {
        // Monday 2026-01-26 through Sunday 2026-02-01 spans a month boundary.
        // The label intentionally follows the current culture; pin one for the
        // format assertion.
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var summary = new WeeklyUsageSummary
            {
                WeekStartDate = new DateTime(2026, 1, 26),
                WeekEndDate = new DateTime(2026, 2, 1)
            };
            Assert.Equal("Jan 26-1", summary.WeekLabel);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void MonthLabel_RendersMonthAndYear()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var summary = new MonthlyUsageSummary { Year = 2026, Month = 11 };
            Assert.Equal("Nov 2026", summary.MonthLabel);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
