using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RubyDevice.Services;

/// <summary>
/// Severity levels for in-app notifications
/// </summary>
public enum NotificationSeverity { Info, Success, Warning }

/// <summary>
/// A single notification to be displayed in the app UI
/// </summary>
public class AppNotification
{
    /// <summary>Short title</summary>
    public string Title { get; init; } = "";
    /// <summary>Message body</summary>
    public string Message { get; init; } = "";
    /// <summary>Severity used to pick the display style</summary>
    public NotificationSeverity Severity { get; init; }
    /// <summary>How long the notification stays visible, in seconds</summary>
    public int DurationSeconds { get; init; } = 5;
}

/// <summary>
/// Notification service. Raises <see cref="NotificationPosted"/> and the app
/// shell (MainWindow) renders it as an in-app InfoBar.
/// </summary>
/// <remarks>
/// System toast notifications require a Start Menu shortcut with an AUMID, which
/// an unpackaged app cannot rely on having, so notifications are shown in-app.
/// </remarks>
public class NotificationService : INotifyPropertyChanged
{
    private static NotificationService? _instance;

    /// <summary>
    /// Gets the notification service singleton instance
    /// </summary>
    public static NotificationService Instance => _instance ??= new NotificationService();

    private bool _enableNotifications = true;

    /// <summary>
    /// Whether notifications should be shown
    /// </summary>
    public bool EnableNotifications
    {
        get => _enableNotifications;
        set
        {
            if (_enableNotifications != value)
            {
                _enableNotifications = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Raised whenever a notification should be displayed. Fired on the calling
    /// thread; MainWindow marshals to the UI thread.
    /// </summary>
    public event EventHandler<AppNotification>? NotificationPosted;

    /// <summary>
    /// Posts an in-app notification
    /// </summary>
    /// <param name="title">Title</param>
    /// <param name="message">Message body</param>
    /// <param name="duration">Display duration in seconds</param>
    public void ShowToast(string title, string message, int duration = 5)
    {
        if (!EnableNotifications) return;

        NotificationPosted?.Invoke(this, new AppNotification
        {
            Title = title,
            Message = message,
            Severity = NotificationSeverity.Info,
            DurationSeconds = duration
        });
    }

    /// <summary>
    /// Posts a device-disabled notification (with auto-restore countdown info)
    /// </summary>
    public void ShowDeviceDisabledNotification(string deviceName, int autoRestoreSeconds)
    {
        if (!EnableNotifications) return;

        var loc = LocalizationService.Instance;
        Post(new AppNotification
        {
            Title = loc["DeviceDisabled"],
            Message = string.Format(loc["AutoRestoreNotification"], deviceName, autoRestoreSeconds),
            Severity = NotificationSeverity.Warning,
            DurationSeconds = autoRestoreSeconds
        });
    }

    /// <summary>
    /// Posts a device-enabled notification
    /// </summary>
    public void ShowDeviceEnabledNotification(string deviceName)
    {
        if (!EnableNotifications) return;

        var loc = LocalizationService.Instance;
        Post(new AppNotification
        {
            Title = loc["DeviceEnabled"],
            Message = deviceName,
            Severity = NotificationSeverity.Success,
            DurationSeconds = 5
        });
    }

    private void Post(AppNotification notification) => NotificationPosted?.Invoke(this, notification);

    /// <summary>
    /// Raised when a notification setting changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the calling member
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
