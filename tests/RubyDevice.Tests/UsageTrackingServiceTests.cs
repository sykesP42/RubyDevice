using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RubyDevice.Tests;

/// <summary>
/// Unit tests for UsageTrackingService logic
/// Note: Tests the pure logic without WinUI dependencies
/// </summary>
public class UsageTrackingServiceTests
{
    #region FormatTime Tests

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(60, "1m")]
    [InlineData(1800, "30m")]
    [InlineData(3599, "59m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(5400, "1h 30m")]
    [InlineData(7200, "2h 0m")]
    [InlineData(9000, "2h 30m")]
    public void FormatTime_ReturnsCorrectFormat(double seconds, string expected)
    {
        var result = FormatTime(seconds);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Weekly Aggregation Tests

    [Fact]
    public void GetWeekStartDate_ForMonday_ReturnsSameDate()
    {
        var date = new DateTime(2026, 7, 6); // Monday
        var result = GetWeekStartDate(date);
        Assert.Equal(date, result);
    }

    [Fact]
    public void GetWeekStartDate_ForWednesday_ReturnsMonday()
    {
        var date = new DateTime(2026, 7, 8); // Wednesday
        var expected = new DateTime(2026, 7, 6); // Monday
        var result = GetWeekStartDate(date);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetWeekStartDate_ForSunday_ReturnsPreviousMonday()
    {
        var date = new DateTime(2026, 7, 5); // Sunday
        var expected = new DateTime(2026, 6, 29); // Previous Monday
        var result = GetWeekStartDate(date);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Activity Rate Calculation Tests

    [Theory]
    [InlineData(3600, 7200, 50.0)]  // 1h active, 2h enabled = 50%
    [InlineData(1800, 3600, 50.0)]  // 30m active, 1h enabled = 50%
    [InlineData(1000, 1000, 100.0)] // Same time = 100%
    [InlineData(0, 3600, 0.0)]      // No active time = 0%
    [InlineData(3600, 0, 0.0)]      // No enabled time = 0%
    public void CalculateActivityRate_ReturnsCorrectPercentage(double activeSeconds, long enabledSeconds, double expected)
    {
        var result = CalculateActivityRate(activeSeconds, enabledSeconds);
        Assert.Equal(expected, result, 1);
    }

    #endregion

    #region Helper Methods (Copied from service for testing)

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private static DateTime GetWeekStartDate(DateTime date)
    {
        // Monday-based week (ISO 8601)
        var dayOfWeek = date.DayOfWeek;
        var daysToSubtract = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        return date.AddDays(-daysToSubtract).Date;
    }

    private static double CalculateActivityRate(double activeSeconds, long enabledSeconds)
    {
        if (enabledSeconds == 0)
            return 0;

        return (activeSeconds / enabledSeconds) * 100;
    }

    #endregion
}
