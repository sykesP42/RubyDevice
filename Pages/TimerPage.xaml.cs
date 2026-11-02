using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

/// <summary>
/// Auto-restore timer page. The countdown itself lives in <see cref="MainViewModel"/>
/// so it keeps running when the user navigates away; this page only displays and
/// controls it.
/// </summary>
public sealed partial class TimerPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _initialized;
    private int _totalMinutes;

    public TimerPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateLocalizedStrings();
        UpdateLocalizedStrings();
        _initialized = true;
        ApplyPresetVisuals();
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
        ToolTipService.SetToolTip(BtnStart, _loc["Start"]);
        ToolTipService.SetToolTip(BtnStop, _loc["Stop"]);

        UpdateTimerStateText();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Detach from any previous view model first, otherwise repeated navigation
        // would stack up duplicate subscriptions on the same instance.
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.AutoRestoreTick -= OnAutoRestoreTick;
        }

        _viewModel = e.Parameter as MainViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.AutoRestoreTick += OnAutoRestoreTick;
        }

        UpdateDisabledList();
        LoadTimerSettings();
        SyncCountdownState();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        // Only detach the UI: the countdown keeps running app-wide.
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.AutoRestoreTick -= OnAutoRestoreTick;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAutoRestoreRunning))
        {
            SyncCountdownState();
            UpdateDisabledList();
        }
        else
        {
            UpdateDisabledList();
        }
    }

    private void OnAutoRestoreTick(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;

        if (_viewModel.IsAutoRestoreRunning)
        {
            UpdateTimerDisplay(_viewModel.AutoRestoreRemainingSeconds);
        }
        UpdateTimerStateText();
        UpdateDisabledList();
    }

    private void LoadTimerSettings()
    {
        var settings = TimerConfig.Load();
        _totalMinutes = Math.Clamp(settings.TimeoutMinutes, 1, 480);
        TimeoutBox.Value = _totalMinutes;
        UpdateTimerDisplay(_totalMinutes * 60);
        ApplyPresetVisuals();
    }

    private void UpdateDisabledList()
    {
        if (_viewModel == null) return;
        var disabled = _viewModel.AllDevices.Where(d => !d.IsEnabled).ToList();
        DisabledList.ItemsSource = disabled;
        DisabledCount.Text = disabled.Count.ToString();
        BtnRestoreAll.Visibility = disabled.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Countdown Controls

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.IsAutoRestoreRunning) return;

        var disabledCount = _viewModel.AllDevices.Count(d => !d.IsEnabled);
        if (disabledCount == 0)
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

        var minutes = (int)Math.Max(1, TimeoutBox.Value);
        if (!_viewModel.StartAutoRestore(minutes)) return;

        // Persist the chosen timeout for the next launch
        new TimerConfig { TimeoutMinutes = minutes }.Save();

        _totalMinutes = minutes;
        UpdateTimerDisplay(_viewModel.AutoRestoreRemainingSeconds);
        UpdateTimerStateText();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.StopAutoRestore();
        UpdateTimerStateText();
        if (_viewModel == null || !_viewModel.IsAutoRestoreRunning)
        {
            UpdateTimerDisplay(_totalMinutes * 60);
        }
    }

    private void BtnRestoreAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var restoredCount = _viewModel.RestoreAllDisabledDevices();
        if (restoredCount > 0)
        {
            var msg = string.Format(_loc["DevicesRestoredCount"], restoredCount);
            NotificationService.Instance.ShowToast(_loc["DeviceRestored"], msg);
        }

        if (_viewModel.IsAutoRestoreRunning)
        {
            _viewModel.StopAutoRestore();
        }

        UpdateTimerDisplay(_totalMinutes * 60);
        UpdateTimerStateText();
        UpdateDisabledList();
    }

    /// <summary>
    /// Aligns the page with the view model's countdown state. The countdown
    /// survives page navigation, so returning to this page re-attaches to it.
    /// </summary>
    private void SyncCountdownState()
    {
        if (_viewModel == null) return;

        var running = _viewModel.IsAutoRestoreRunning;
        BtnStart.IsEnabled = !running;
        BtnStop.IsEnabled = running;
        TimeoutBox.IsEnabled = !running;

        if (running)
        {
            UpdateTimerDisplay(_viewModel.AutoRestoreRemainingSeconds);
        }
        else
        {
            UpdateTimerDisplay(_totalMinutes * 60);
        }
        UpdateTimerStateText();
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// Renders the remaining time as MM:SS.
    /// </summary>
    private void UpdateTimerDisplay(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        var mins = totalSeconds / 60;
        var secs = totalSeconds % 60;
        TimerDisplay.Text = $"{mins:D2}:{secs:D2}";
    }

    /// <summary>
    /// Updates the state label and indicator dot to reflect whether the timer is running.
    /// </summary>
    private void UpdateTimerStateText()
    {
        var running = _viewModel?.IsAutoRestoreRunning ?? false;
        TimerState.Text = running ? _loc["TimerRunning"] : _loc["TimerIdle"];
        StateIndicator.Background = running
            ? (Brush)Application.Current.Resources["SuccessBrush"]
            : (Brush)Application.Current.Resources["TextTertiaryBrush"];
    }

    #endregion

    #region Event Handlers

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { IsAutoRestoreRunning: true }) return;

        if (sender is Button btn && btn.Tag is string minutes && int.TryParse(minutes, out var mins))
        {
            TimeoutBox.Value = mins;
            _totalMinutes = mins;
            UpdateTimerDisplay(mins * 60);
            ApplyPresetVisuals();
        }
    }

    /// <summary>
    /// Highlights the preset matching the current timeout so the selection is visible
    /// </summary>
    private void ApplyPresetVisuals()
    {
        var buttons = new[] { Preset5, Preset15, Preset30, Preset60, Preset120 };
        var primary = (Brush)Application.Current.Resources["PrimaryBrush"];
        var primaryLight = (Brush)Application.Current.Resources["PrimaryLightBrush"];
        var border = (Brush)Application.Current.Resources["BorderBrush"];
        var secondary = (Brush)Application.Current.Resources["TextSecondaryBrush"];
        var textPrimary = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        foreach (var btn in buttons)
        {
            var selected = btn.Tag is string tag && int.TryParse(tag, out var m) && m == _totalMinutes;
            btn.Background = selected ? primaryLight : transparent;
            btn.BorderBrush = selected ? primary : border;
            btn.BorderThickness = new Thickness(1);
            btn.Foreground = selected ? textPrimary : secondary;
        }
    }

    private void TimeoutBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_initialized) return;
        if (_viewModel is { IsAutoRestoreRunning: true }) return;
        if (args.NewValue <= 0) return;

        _totalMinutes = (int)args.NewValue;
        UpdateTimerDisplay(_totalMinutes * 60);
        ApplyPresetVisuals();
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
            UpdateDisabledList();
        }
        else if (!wantEnable && wasEnabled)
        {
            toggle.IsOn = wasEnabled;
            await _viewModel.DisableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
            UpdateDisabledList();
        }
    }

    #endregion
}
