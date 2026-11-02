using System;
using RubyDevice.Helpers;
using RubyDevice.Models;
using Xunit;

namespace RubyDevice.Tests;

/// <summary>
/// Tests for the real <see cref="TimeHelper"/> and the usage model display
/// properties.
/// </summary>
public class TimeHelperTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(59, "0m")]      // fractional input truncates toward zero
    [InlineData(60, "1m")]
    [InlineData(1800, "30m")]
    [InlineData(3599, "59m")]
    [InlineData(3600, "1h 0m")]
    [InlineData(5400, "1h 30m")]
    [InlineData(7200, "2h 0m")]
    [InlineData(9000, "2h 30m")]
    [InlineData(86399, "23h 59m")]
    [InlineData(86400, "24h 0m")]
    public void FormatTime_ReturnsCorrectFormat(double seconds, string expected)
    {
        Assert.Equal(expected, TimeHelper.FormatTime(seconds));
    }

    [Theory]
    [InlineData(0.9, "0m")]
    [InlineData(3600.9, "1h 0m")]
    public void FormatTime_TruncatesFractionalSeconds(double seconds, string expected)
    {
        Assert.Equal(expected, TimeHelper.FormatTime(seconds));
    }
}
