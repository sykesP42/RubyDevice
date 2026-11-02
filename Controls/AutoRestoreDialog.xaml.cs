using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

/// <summary>
/// Second-stage dialog shown after a device was disabled. Counts down and, unless
/// the user keeps the device disabled, asks the caller to restore it.
/// </summary>
public sealed partial class AutoRestoreDialog : ContentDialog
{
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;
    private bool _decided;

    public string DialogTitle { get; }
    public string DeviceName { get; }
    public string CountdownLabel { get; }
    public string HintText { get; }
    public string KeepDisabledText { get; }
    public string RestoreText { get; }

    /// <summary>
    /// Creates the auto-restore countdown dialog
    /// </summary>
    /// <param name="deviceName">Device name</param>
    /// <param name="countdownSeconds">Countdown seconds</param>
    public AutoRestoreDialog(string deviceName, int countdownSeconds)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["DeviceDisabledTitle"];
        DeviceName = string.Format(loc["DeviceDisabledMessage"], deviceName);
        CountdownLabel = loc["AutoRestoreCountdown"];
        HintText = loc["AutoRestoreHintDialog"];
        KeepDisabledText = loc["KeepDisabled"];
        RestoreText = loc["RestoreNow"];

        _remainingSeconds = countdownSeconds;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        UpdateCountdownText();
    }

    /// <summary>
    /// Shows the dialog and runs the countdown.
    /// </summary>
    /// <returns>True when the device should be restored (countdown expired or
    /// user chose "Restore Now"), false when the user chose "Keep Disabled"</returns>
    public async Task<bool> ShowAndCountdownAsync()
    {
        _timer.Start();
        var result = await ShowAsync();
        _timer.Stop();

        if (_decided)
        {
            // Countdown expired and already hid the dialog
            return true;
        }

        _decided = true;
        // Primary button = "Keep Disabled"; anything else restores
        return result != ContentDialogResult.Primary;
    }

    private void Timer_Tick(object? sender, object e)
    {
        _remainingSeconds--;
        UpdateCountdownText();

        if (_remainingSeconds > 0 || _decided) return;

        _decided = true;
        _timer.Stop();
        Hide();
    }

    private void UpdateCountdownText()
    {
        CountdownText.Text = $"{_remainingSeconds}s";
    }
}
