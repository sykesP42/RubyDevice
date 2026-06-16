using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

public sealed partial class AutoRestoreDialog : ContentDialog
{
    private readonly int _countdownSeconds;
    private readonly Action _onRestore;
    private readonly Action _onKeepDisabled;
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;

    public string DialogTitle { get; }
    public string DeviceName { get; }
    public string CountdownLabel { get; }
    public string HintText { get; }
    public string KeepDisabledText { get; }
    public string RestoreText { get; }

    /// <summary>
    /// 创建自动恢复倒计时弹窗
    /// </summary>
    /// <param name="deviceName">设备名称</param>
    /// <param name="countdownSeconds">倒计时秒数</param>
    /// <param name="onRestore">恢复设备操作</param>
    /// <param name="onKeepDisabled">保持禁用操作</param>
    public AutoRestoreDialog(string deviceName, int countdownSeconds, Action onRestore, Action onKeepDisabled)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["DeviceDisabledTitle"];
        DeviceName = string.Format(loc["DeviceDisabledMessage"], deviceName);
        CountdownLabel = loc["AutoRestoreCountdown"];
        HintText = loc["AutoRestoreHintDialog"];
        KeepDisabledText = loc["KeepDisabled"];
        RestoreText = loc["RestoreNow"];

        _countdownSeconds = countdownSeconds;
        _onRestore = onRestore;
        _onKeepDisabled = onKeepDisabled;
        _remainingSeconds = countdownSeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateCountdownText();
    }

    public async Task ShowAndCountdownAsync()
    {
        _timer.Start();
        var result = await ShowAsync();
        _timer.Stop();

        if (result == ContentDialogResult.Primary)
        {
            // 用户选择保持禁用
            _onKeepDisabled?.Invoke();
        }
        else
        {
            // 用户选择立即恢复或倒计时结束
            _onRestore?.Invoke();
        }
    }

    private void Timer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateCountdownText();

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            Hide();
            _onRestore?.Invoke();
        }
    }

    private void UpdateCountdownText()
    {
        CountdownText.Text = $"{_remainingSeconds}s";
    }
}