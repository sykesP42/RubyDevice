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
    public long EnabledSeconds { get; set; }

    /// <summary>
    /// Date key for dictionary storage (YYYY-MM-DD format)
    /// </summary>
    public string DateKey => Date.ToString("yyyy-MM-dd");
}

/// <summary>
/// Collection of usage records for JSON serialization
/// </summary>
public class UsageDataFile
{
    public System.Collections.Generic.List<DeviceUsageRecord> Records { get; set; } = new();
}
