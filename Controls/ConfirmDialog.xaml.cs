using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

public sealed partial class ConfirmDialog : ContentDialog
{
    private readonly int _timeoutSeconds;
    private readonly Action _onConfirm;
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;

    public string DialogTitle { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    /// <summary>
    /// 创建确认对话框
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息</param>
    /// <param name="timeoutSeconds">倒计时秒数</param>
    /// <param name="onConfirm">用户确认时执行的操作（倒计时结束不执行此操作）</param>
    public ConfirmDialog(string title, string message, int timeoutSeconds, Action onConfirm)
    {
        InitializeComponent();

        DialogTitle = title;
        Message = message;

        var loc = LocalizationService.Instance;
        ConfirmText = loc["ConfirmSave"];
        CancelText = loc["Cancel"];

        _timeoutSeconds = timeoutSeconds;
        _onConfirm = onConfirm;
        _remainingSeconds = timeoutSeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateCountdownText();
    }

    public async Task ShowWithTimeoutAsync()
    {
        _timer.Start();
        var result = await ShowAsync();
        _timer.Stop();

        // 只有用户点击确认按钮才执行操作
        if (result == ContentDialogResult.Primary)
        {
            _onConfirm?.Invoke();
        }
        // 其他情况（取消、超时关闭）不执行任何操作，设备保持启用状态
    }

    private void Timer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateCountdownText();

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            // 倒计时结束，关闭对话框但不执行操作（设备保持启用）
            Hide();
        }
    }

    private void UpdateCountdownText()
    {
        var loc = LocalizationService.Instance;
        CountdownText.Text = $"{_remainingSeconds} {loc["SecondsRemaining"]}";
    }
}