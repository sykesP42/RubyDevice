using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

public sealed partial class DisableConfirmDialog : ContentDialog
{
    private readonly int _countdownSeconds;
    private readonly Action _onConfirm;
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;

    public string DialogTitle { get; }
    public string Message { get; }
    public string CancelText { get; }
    public string CountdownLabel { get; }
    public string AutoRestoreHint { get; }

    /// <summary>
    /// 创建禁用设备确认弹窗（第一阶段：5秒倒计时确认）
    /// </summary>
    /// <param name="deviceName">设备名称</param>
    /// <param name="countdownSeconds">倒计时秒数</param>
    /// <param name="onConfirm">确认后执行的操作</param>
    public DisableConfirmDialog(string deviceName, int countdownSeconds, Action onConfirm)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["DisableConfirmTitle"];
        Message = loc["DisableConfirmMessage"];
        CancelText = loc["Cancel"];
        CountdownLabel = loc["DisablingIn"];
        AutoRestoreHint = loc["AutoRestoreHint"];

        _countdownSeconds = countdownSeconds;
        _onConfirm = onConfirm;
        _remainingSeconds = countdownSeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateCountdownText();
    }

    /// <summary>
    /// 显示弹窗并开始倒计时
    /// </summary>
    /// <returns>true 表示用户确认或倒计时结束，false 表示用户取消</returns>
    public async Task<bool> ShowWithCountdownAsync()
    {
        _timer.Start();
        var result = await ShowAsync();
        _timer.Stop();

        // 用户点击关闭按钮（取消）返回 false
        // 倒计时结束会自动调用 _onConfirm 并返回 true
        return result != ContentDialogResult.None || _remainingSeconds <= 0;
    }

    private void Timer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateCountdownText();

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            // 倒计时结束，执行禁用操作
            Hide();
            _onConfirm?.Invoke();
        }
    }

    private void UpdateCountdownText()
    {
        CountdownText.Text = $"{_remainingSeconds}s";
    }
}