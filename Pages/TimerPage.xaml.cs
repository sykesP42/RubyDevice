using System;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Core;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

/// <summary>
/// Auto-restore timer page. Counts down and re-enables all disabled devices
/// when the timeout expires. Uses a System.Threading.Timer with 1s ticks.
/// </summary>
public sealed partial class TimerPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _initialized = false;

    // Countdown timer
    private Timer? _countdownTimer;
    private int _remainingSeconds;
    private int _totalMinutes;
    private bool _isRunning;

    // Timer settings persistence

    public TimerPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateLocalizedStrings();
        UpdateLocalizedStrings();
        _initialized = true;
    }

    private void UpdateLocalizedStrings()
    {
        TimerTitle.Text = _loc["AutoRestoreTimer"];
        TimerDesc.Text = _loc["AutoRestoreDesc"];
        TimeoutBox.Header = _loc["TimeoutMinutes"];
        PresetsTitle.Text = _loc["QuickPresets"];
        DisabledTitle.Text = _loc["DisabledDevicesList"];
        DisabledDesc.Text = _loc["DisabledListDesc"];
        TextStart.Text = _loc["Start"];
        TextStop.Text = _loc["Stop"];
        TextRestoreAll.Text = _loc["RestoreAll"];
        TimerUnitText.Text = _loc["Minutes"];

        UpdateTimerStateText();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += (_, _) => UpdateDisabledList();
        }

        UpdateDisabledList();
        LoadTimerSettings();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopCountdown();
    }

    private void LoadTimerSettings()
    {
        var settings = TimerConfig.Load();
        TimeoutBox.Value = settings.TimeoutMinutes;
        _totalMinutes = settings.TimeoutMinutes;
        UpdateTimerDisplay(settings.TimeoutMinutes * 60);
    }

    private void SaveTimerSettings()
    {
        new TimerConfig { TimeoutMinutes = _totalMinutes }.Save();
    }

    private void UpdateDisabledList()
    {
        if (_viewModel == null) return;
        var disabled = _viewModel.AllDevices.Where(d => !d.IsEnabled).ToList();
        DisabledList.ItemsSource = disabled;
        DisabledCount.Text = disabled.Count.ToString();
        BtnRestoreAll.Visibility = disabled.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Countdown Logic

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning || _viewModel == null) return;

        var disabled = _viewModel.AllDevices.Where(d => !d.IsEnabled).ToList();
        if (disabled.Count == 0)
        {
            // No devices to restore
            var dialog = new ContentDialog
            {
                Title = _loc["AutoRestoreTimer"],
                Content = _loc["NoDevicesToRestore"],
                CloseButtonText = _loc["OK"],
                XamlRoot = this.XamlRoot
            };
            _ = dialog.ShowAsync();
            return;
        }

        _totalMinutes = (int)Math.Max(1, TimeoutBox.Value);
        _remainingSeconds = _totalMinutes * 60;

        StartCountdown();
        SaveTimerSettings();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        StopCountdown();
    }

    private void StartCountdown()
    {
        _isRunning = true;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        TimeoutBox.IsEnabled = false;
        UpdateTimerDisplay(_remainingSeconds);
        UpdateTimerStateText();

        // Start countdown timer (fires every 1 second)
        _countdownTimer?.Dispose();
        _countdownTimer = new Timer(OnCountdownTick, null, 0, 1000);
    }

    private void StopCountdown()
    {
        _isRunning = false;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        TimeoutBox.IsEnabled = true;
        UpdateTimerDisplay(_totalMinutes * 60);
        UpdateTimerStateText();
    }

    private void OnCountdownTick(object? state)
    {
        if (!_isRunning) return;

        _remainingSeconds--;

        // Update UI on UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isRunning) return;
            UpdateTimerDisplay(_remainingSeconds);

            if (_remainingSeconds <= 0)
            {
                // Timer complete - restore all disabled devices
                RestoreAllDevices();
                StopCountdown();
            }
        });
    }

    private void RestoreAllDevices()
    {
        if (_viewModel == null) return;

        var deviceManager = _viewModel.GetDeviceManager();
        var disabled = _viewModel.AllDevices.Where(d => !d.IsEnabled).ToList();
        var restoredCount = 0;

        foreach (var device in disabled)
        {
            if (deviceManager.ToggleDevice(device.DeviceId, true))
            {
                device.IsEnabled = true;
                restoredCount++;
            }
        }

        // Notification
        if (restoredCount > 0)
        {
            var msg = $"{restoredCount} device(s) restored";
            var notifyService = NotificationService.Instance;
            notifyService.ShowToast(_loc["DeviceRestored"], msg);

            UpdateDisabledList();
        }
    }

    private void BtnRestoreAll_Click(object sender, RoutedEventArgs e)
    {
        RestoreAllDevices();

        if (_isRunning)
        {
            StopCountdown();
        }
    }

    #endregion

    #region UI Updates

    private void UpdateTimerDisplay(int totalSeconds)
    {
        var mins = totalSeconds / 60;
        var secs = totalSeconds % 60;
        TimerDisplay.Text = $"{mins:D2}:{secs:D2}";
    }

    private void UpdateTimerStateText()
    {
        if (_isRunning)
        {
            TimerState.Text = _loc["TimerRunning"];
            StateIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 34, 197, 94)); // Green
        }
        else
        {
            TimerState.Text = _loc["TimerIdle"];
            StateIndicator.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 148, 163, 184)); // Gray
        }
    }

    #endregion

    #region Event Handlers

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        if (sender is Button btn && btn.Tag is string minutes)
        {
            var mins = int.Parse(minutes);
            TimeoutBox.Value = mins;
            _totalMinutes = mins;
            UpdateTimerDisplay(mins * 60);
        }
    }

    private void TimeoutBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_initialized || _isRunning) return;
        if (args.NewValue > 0)
        {
            _totalMinutes = (int)args.NewValue;
            UpdateTimerDisplay(_totalMinutes * 60);
        }
    }

    private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle ||
            toggle.DataContext is not DeviceViewModel device ||
            _viewModel == null) return;

        bool wasEnabled = device.IsEnabled;
        bool wantEnable = toggle.IsOn;

        if (wantEnable && !wasEnabled)
        {
            toggle.IsOn = wasEnabled;
            await _viewModel.EnableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
        else if (!wantEnable && wasEnabled)
        {
            toggle.IsOn = wasEnabled;
            await _viewModel.DisableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
    }

    #endregion
}
