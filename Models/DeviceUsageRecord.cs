using System;

namespace RubyDevice.Models;

/// <summary>
/// Single day's usage record for one device
/// </summary>
public class DeviceUsageRecord
{
    public string DeviceId { get; set; } = "";
    public DateTime Date { get; set; }
    public double ActiveSeconds { get; set; }
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
