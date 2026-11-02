using System;
using System.Management;
using System.Threading;

namespace RubyDevice.Services;

/// <summary>
/// Monitors device connection/disconnection events using Windows Management Instrumentation (WMI).
/// Listens for Win32_DeviceChangeEvent to detect when input devices are added or removed,
/// then fires a debounced notification so the UI can refresh the device list.
/// </summary>
/// <remarks>
/// <see cref="DevicesChanged"/> is raised from a thread-pool thread by the debounce
/// timer, so subscribers must marshal to the UI thread themselves. Call
/// <see cref="Dispose"/> on shutdown to stop the watcher and release the timer,
/// otherwise a pending callback can still fire after the UI is gone.
/// </remarks>
public class DeviceWatcherService : IDisposable
{
    private static DeviceWatcherService? _instance;

    /// <summary>
    /// Gets the singleton instance of the DeviceWatcherService.
    /// </summary>
    public static DeviceWatcherService Instance => _instance ??= new DeviceWatcherService();

    private ManagementEventWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new();
    private bool _disposed;

    /// <summary>
    /// Raised when device changes are detected.
    /// Events are debounced with a 1-second delay to avoid rapid successive notifications.
    /// </summary>
    public event EventHandler? DevicesChanged;

    private DeviceWatcherService() { }

    /// <summary>
    /// Starts monitoring device plug-and-play events via WMI.
    /// If already running, stops and restarts to ensure fresh monitoring state.
    /// Safe to call multiple times.
    /// </summary>
    public void Start()
    {
        if (_watcher != null)
        {
            Stop();
        }

        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnDeviceEvent;
            _watcher.Start();
        }
        catch
        {
            // WMI may be unavailable in constrained environments (e.g. some containers, WinPE).
            // Device detection is best-effort; silent fail allows the app to run without it.
        }
    }

    /// <summary>
    /// Stops monitoring device changes and releases the WMI event watcher.
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
        catch
        {
            // Best-effort cleanup
        }
    }

    /// <summary>
    /// Handles WMI device change events with 1-second debouncing to coalesce
    /// multiple rapid events (e.g. when a composite device enumerates several interfaces).
    /// </summary>
    private void OnDeviceEvent(object? sender, EventArrivedEventArgs e)
    {
        if (_disposed) return;

        // WMI callbacks can interleave, so the debounce timer replacement is
        // guarded; otherwise a discarded timer could survive and fire an
        // extra DevicesChanged event.
        Timer timer;
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            timer = new Timer(_ =>
            {
                if (_disposed) return;
                DevicesChanged?.Invoke(this, EventArgs.Empty);
            }, null, 1000, Timeout.Infinite);
            _debounceTimer = timer;
        }
    }

    /// <summary>
    /// Releases all managed resources. Stops the WMI watcher and disposes the debounce timer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounceTimer?.Dispose();
    }
}
