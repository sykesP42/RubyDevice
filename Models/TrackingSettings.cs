using System;
using System.Collections.Generic;

namespace RubyDevice.Models;

/// <summary>
/// Per-device tracking configuration
/// </summary>
/// <summary>
/// Per-device tracking configuration
/// </summary>
public class DeviceTrackingSetting
{
    /// <summary>Device identifier</summary>
    public string DeviceId { get; set; } = "";
    /// <summary>Whether usage tracking is enabled for this device</summary>
    public bool IsTrackingEnabled { get; set; }
    /// <summary>Date tracking was first enabled, used for tracking duration display</summary>
    public DateTime? FirstTrackedDate { get; set; }
}

/// <summary>
/// Global tracking configuration
/// </summary>
public class TrackingConfig
{
    /// <summary>Number of days to retain usage records (7-365)</summary>
    public int RetentionDays { get; set; } = 30;
    /// <summary>Whether to automatically clean up records older than RetentionDays</summary>
    public bool AutoCleanup { get; set; } = true;
}

/// <summary>
/// Complete tracking settings file structure for JSON serialization
/// </summary>
public class TrackingSettingsFile
{
    /// <summary>Global tracking configuration</summary>
    public TrackingConfig Config { get; set; } = new();
    /// <summary>Per-device tracking settings keyed by device ID</summary>
    public Dictionary<string, DeviceTrackingSetting> Devices { get; set; } = new();
}
