using System;

namespace RubyDevice.Models;

/// <summary>
/// Single day's usage record for one device
/// </summary>
public class DeviceUsageRecord
{
    /// <summary>Device identifier the record belongs to</summary>
    public string DeviceId { get; set; } = "";
    /// <summary>Date of the usage record (date only, no time component)</summary>
    public DateTime Date { get; set; }
    /// <summary>Total active (in-use) seconds on this date</summary>
    public double ActiveSeconds { get; set; }
    /// <summary>Total enabled seconds on this date</summary>
    public double EnabledSeconds { get; set; }

    /// <summary>
    /// Date key for dictionary storage (YYYY-MM-DD format)
    /// </summary>
    /// <remarks>
    /// Formatted with the current culture's calendar, so the year/month/day
    /// components assume a Gregorian calendar.
    /// </remarks>
    public string DateKey => Date.ToString("yyyy-MM-dd");
}

/// <summary>
/// Collection of usage records for JSON serialization
/// </summary>
public class UsageDataFile
{
    /// <summary>All persisted usage records, across every device and date</summary>
    public System.Collections.Generic.List<DeviceUsageRecord> Records { get; set; } = new();
}
