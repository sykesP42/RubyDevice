using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace RubyDevice.Services;

/// <summary>
/// Monitors device connection/disconnection events using WMI
/// </summary>
public class DeviceWatcherService : IDisposable
{
    private static DeviceWatcherService? _instance;
    public static DeviceWatcherService Instance => _instance ??= new DeviceWatcherService();

    private ManagementEventWatcher? _watcher;
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>
    /// Fires when device changes are detected (debounced)
    /// </summary>
    public event EventHandler? DevicesChanged;

    private DeviceWatcherService() { }

    /// <summary>
    /// Start monitoring device changes.
    /// Watches for Win32_DeviceChangeEvent (device arrival/removal).
    /// </summary>
    public void Start()
    {
        if (_watcher != null) return;

        try
        {
            // Watch for device arrival/removal events
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnDeviceEvent;
            _watcher.Start();
        }
        catch
        {
            // WMI might not be available in all environments
            // Silent fail - device detection is a best-effort feature
        }
    }

    /// <summary>
    /// Stop monitoring device changes
    /// </summary>
    public void Stop()
    {
        if (_watcher == null) return;

        try
        {
            _watcher.Stop();
            _watcher.EventArrived -= OnDeviceEvent;
            _watcher.Dispose();
            _watcher = null;
        }
        catch { }
    }

    /// <summary>
    /// Handle WMI device events with debouncing
    /// </summary>
    private void OnDeviceEvent(object? sender, EventArrivedEventArgs e)
    {
        if (_disposed) return;

        // Debounce: wait 1 second to avoid multiple rapid events
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            if (_disposed) return;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }, null, 1000, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounceTimer?.Dispose();
    }
}
