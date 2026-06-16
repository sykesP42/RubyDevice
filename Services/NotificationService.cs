using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace RubyDevice.Services;

/// <summary>
/// 通知服务 - 管理应用内的Toast通知
/// </summary>
public class NotificationService : INotifyPropertyChanged
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new NotificationService();

    private bool _enableNotifications = true;

    /// <summary>
    /// 是否启用通知
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
    /// 显示Toast通知
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    /// <param name="duration">显示时长（秒）</param>
    public void ShowToast(string title, string message, int duration = 5)
    {
        if (!EnableNotifications) return;

        try
        {
            string toastXml = $@"
                <toast duration='short'>
                    <visual>
                        <binding template='ToastText02'>
                            <text id='1'>{EscapeXml(title)}</text>
                            <text id='2'>{EscapeXml(message)}</text>
                        </binding>
                    </visual>
                </toast>";

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(toastXml);

            var toastNotification = new ToastNotification(xmlDoc);
            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);
        }
        catch
        {
            // 如果Toast通知失败，静默处理
        }
    }

    /// <summary>
    /// 显示设备禁用通知（带自动恢复倒计时）
    /// </summary>
    public void ShowDeviceDisabledNotification(string deviceName, int autoRestoreSeconds)
    {
        if (!EnableNotifications) return;

        var loc = LocalizationService.Instance;
        ShowToast(
            loc["DeviceDisabled"],
            string.Format(loc["AutoRestoreNotification"], deviceName, autoRestoreSeconds),
            10
        );
    }

    /// <summary>
    /// 显示设备启用通知
    /// </summary>
    public void ShowDeviceEnabledNotification(string deviceName)
    {
        if (!EnableNotifications) return;

        var loc = LocalizationService.Instance;
        ShowToast(loc["DeviceEnabled"], deviceName);
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}