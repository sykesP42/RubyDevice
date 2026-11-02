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
    public double TotalEnabledSeconds { get; set; }

    /// <summary>
    /// Number of days with recorded data in this week
    /// </summary>
    public int DaysWithData { get; set; }

    /// <summary>
    /// Week label for display (e.g., "Jan 1-7")
    /// </summary>
    /// <remarks>
    /// Only the start date carries a month, so a week spanning a month boundary
    /// renders as e.g. "Jan 29-4".
    /// </remarks>
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
    public double TotalEnabledSeconds { get; set; }

    /// <summary>
    /// Number of days with recorded data in this month
    /// </summary>
    public int DaysWithData { get; set; }

    /// <summary>
    /// Month label for display (e.g., "Jan 2026")
    /// </summary>
    /// <remarks>
    /// A date is constructed purely to format the label, so <see cref="Year"/> and
    /// <see cref="Month"/> must already hold valid values — an out-of-range month
    /// (0 or 13) throws <see cref="ArgumentOutOfRangeException"/>.
    /// </remarks>
    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}