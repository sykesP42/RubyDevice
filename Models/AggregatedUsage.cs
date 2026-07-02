using System;

namespace RubyDevice.Models;

/// <summary>
/// Weekly aggregated usage statistics for a device
/// </summary>
public class WeeklyUsageSummary
{
    /// <summary>
    /// Monday of the week
    /// </summary>
    public DateTime WeekStartDate { get; set; }

    /// <summary>
    /// Sunday of the week
    /// </summary>
    public DateTime WeekEndDate { get; set; }

    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// Total active seconds during the week
    /// </summary>
    public double TotalActiveSeconds { get; set; }

    /// <summary>
    /// Total enabled seconds during the week
    /// </summary>
    public long TotalEnabledSeconds { get; set; }

    /// <summary>
    /// Number of days with recorded data in this week
    /// </summary>
    public int DaysWithData { get; set; }

    /// <summary>
    /// Week label for display (e.g., "Jan 1-7")
    /// </summary>
    public string WeekLabel => WeekStartDate.ToString("MMM d") + "-" + WeekEndDate.Day;
}

/// <summary>
/// Monthly aggregated usage statistics for a device
/// </summary>
public class MonthlyUsageSummary
{
    /// <summary>
    /// Year of the month
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Month number (1-12)
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// Total active seconds during the month
    /// </summary>
    public double TotalActiveSeconds { get; set; }

    /// <summary>
    /// Total enabled seconds during the month
    /// </summary>
    public long TotalEnabledSeconds { get; set; }

    /// <summary>
    /// Number of days with recorded data in this month
    /// </summary>
    public int DaysWithData { get; set; }

    /// <summary>
    /// Month label for display (e.g., "Jan 2026")
    /// </summary>
    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}