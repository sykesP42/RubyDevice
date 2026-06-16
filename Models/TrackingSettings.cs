using System;
using System.Collections.Generic;

namespace RubyDevice.Models;

/// <summary>
/// Per-device tracking configuration
/// </summary>
public class DeviceTrackingSetting
{
    public string DeviceId { get; set; } = "";
    public bool IsTrackingEnabled { get; set; }
    public DateTime? FirstTrackedDate { get; set; }
}

/// <summary>
/// Global tracking configuration
/// </summary>
public class TrackingConfig
{
    public int RetentionDays { get; set; } = 30;
    public bool AutoCleanup { get; set; } = true;
}

/// <summary>
/// Complete tracking settings file structure
/// </summary>
public class TrackingSettingsFile
{
    public TrackingConfig Config { get; set; } = new();
    public Dictionary<string, DeviceTrackingSetting> Devices { get; set; } = new();
}
