using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

/// <summary>
/// First-stage confirmation dialog for disabling a device. The user can confirm
/// immediately via the primary button, cancel, or wait out the countdown which
/// auto-confirms. The countdown doubles as a determinate progress ring.
/// </summary>
public sealed partial class DisableConfirmDialog : ContentDialog
{
    private readonly DispatcherTimer _timer;
    private readonly int _totalSeconds;
    private int _remainingSeconds;
    private bool _autoConfirmed;

    public string DialogTitle { get; }
    public string DeviceName { get; }
    public string Message { get; }
    public string CancelText { get; }
    public string ConfirmText { get; }
    public string CountdownLabel { get; }
    public string AutoRestoreHint { get; }

    /// <summary>
    /// Creates the disable confirmation dialog (5-second countdown stage)
    /// </summary>
    /// <param name="deviceName">Device name</param>
    /// <param name="countdownSeconds">Countdown seconds</param>
    public DisableConfirmDialog(string deviceName, int countdownSeconds)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["DisableConfirmTitle"];
        DeviceName = deviceName;
        Message = string.Format(loc["DisableConfirmMessage"], deviceName);
        CancelText = loc["Cancel"];
        ConfirmText = loc["Confirm"];
        CountdownLabel = loc["DisablingIn"];
        AutoRestoreHint = loc["AutoRestoreHint"];

        _totalSeconds = countdownSeconds;
        _remainingSeconds = countdownSeconds;

        CountdownRing.Maximum = _totalSeconds;
        CountdownRing.Value = _totalSeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateCountdownText();
    }

    /// <summary>
    /// Shows the dialog and starts the countdown.
    /// </summary>
    /// <returns>True when the user confirmed (button or countdown expiry), false on cancel</returns>
    public async Task<bool> ShowWithCountdownAsync()
    {
        _timer.Start();
        var result = await ShowAsync();
        _timer.Stop();

        return result == ContentDialogResult.Primary || _autoConfirmed;
    }

    private void Timer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateCountdownText();

        if (_remainingSeconds > 0 || _autoConfirmed) return;

        _autoConfirmed = true;
        _timer.Stop();
        Hide();
    }

    private void UpdateCountdownText()
    {
        CountdownText.Text = $"{_remainingSeconds}s";
        CountdownRing.Value = _remainingSeconds;
    }
}
