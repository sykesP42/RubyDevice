using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using RubyDevice.Core;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

/// <summary>
/// Main device list page with type/status filters, search, sort,
/// batch enable/disable, and per-device right-click context menu.
/// </summary>
public sealed partial class DevicesPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public MainViewModel? ViewModel => _viewModel;

    public DevicesPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        TextFilterAll.Text = _loc["All"];
        TextKeyboard.Text = _loc["Keyboards"];
        TextMouse.Text = _loc["Mice"];
        TextTouchpad.Text = _loc["Touchpads"];
        TextRefresh.Text = _loc["Refresh"];
        ToolTipService.SetToolTip(BtnRefresh, _loc["RefreshHint"]);
        TextActivityHighlight.Text = _loc["ActivityHighlight"];
        TextStatusAll.Text = _loc["All"];
        TextStatusEnabled.Text = _loc["Enabled"];
        TextStatusDisabled.Text = _loc["Disabled"];
        SortByName.Text = _loc["SortByName"];
        SortByType.Text = _loc["SortByType"];
        SortByStatus.Text = _loc["SortByStatus"];
        SearchBox.PlaceholderText = _loc["SearchPlaceholder"];
        EmptyStateText.Text = _loc["NoDevicesFound"];
    }

    // Pulsing "live input" dot animations, one per rendered device row. Rows are
    // created per group rebuild, so stories are tracked by row element.
    private sealed class PulseState
    {
        public Storyboard Story = null!;
    }

    private readonly Dictionary<Border, PulseState> _pulseStates = new();

    /// <summary>
    /// Starts a breathing opacity animation on the row's live-input dot. The dot
    /// is only visible while the device is active, and the animation keeps
    /// running underneath so the pulse is already in motion when input arrives.
    /// </summary>
    private void DeviceRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border) return;

        var dot = FindDescendantByName(border, "ActiveDot") as Microsoft.UI.Xaml.Shapes.Ellipse;
        if (dot == null) return;

        var story = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.25,
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = new Microsoft.UI.Xaml.Duration(TimeSpan.FromMilliseconds(850))
        };
        Storyboard.SetTarget(anim, dot);
        Storyboard.SetTargetProperty(anim, "Opacity");
        story.Children.Add(anim);
        story.Begin();

        _pulseStates[border] = new PulseState { Story = story };
    }

    private void DeviceRow_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border) return;
        if (_pulseStates.Remove(border, out var state))
        {
            state.Story.Stop();
        }
    }

    private static DependencyObject? FindDescendantByName(DependencyObject root, string name)
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is Microsoft.UI.Xaml.FrameworkElement fe && fe.Name == name) return child;
            var found = FindDescendantByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;

        // Sync toggle state with view model
        if (_viewModel != null)
        {
            ActivityHighlightToggle.IsOn = _viewModel.IsActivityHighlightEnabled;
        }

        Bindings.Update();
        UpdateDeviceCount();
    }

    private void UpdateDeviceCount()
    {
        if (_viewModel == null) return;
        var count = _viewModel.AllDevices.Count;
        TextDeviceCount.Text = string.Format(_loc["DeviceCount"], count);
        TextDeviceCount.Visibility = Visibility.Visible;
        TextNoDevices.Visibility = _viewModel.DeviceGroups.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || sender is not RadioButton rb) return;
        _viewModel.FilterType = rb.Tag switch
        {
            "Keyboard" => DeviceType.Keyboard,
            "Mouse" => DeviceType.Mouse,
            "Touchpad" => DeviceType.Touchpad,
            _ => null
        };
    }

    private void StatusFilter_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || sender is not RadioButton rb) return;
        _viewModel.StatusFilter = rb.Tag as string ?? "All";
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_viewModel != null)
            _viewModel.SearchText = args.QueryText ?? "";
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && _viewModel != null)
            _viewModel.SearchText = sender.Text ?? "";
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SortMode = SortCombo.SelectedIndex;
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Refresh();
        UpdateDeviceCount();
    }

    private void ActivityHighlight_Toggled(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsActivityHighlightEnabled = ActivityHighlightToggle.IsOn;
            if (!ActivityHighlightToggle.IsOn)
            {
                _viewModel.ActiveDeviceId = null;
            }
        }
    }

    /// <summary>
    /// Opens the device detail page on row press. Presses that start on the
    /// inline toggle switch do not navigate.
    /// </summary>
    private void Device_Click(object sender, PointerRoutedEventArgs e)
    {
        if (IsSourceWithinToggle(e.OriginalSource as DependencyObject)) return;

        if (sender is Border border && border.Tag is DeviceViewModel device && _viewModel != null)
        {
            Frame.Navigate(typeof(DeviceDetailPage), (device, _viewModel));
        }
    }

    private static bool IsSourceWithinToggle(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ToggleSwitch) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    /// <summary>
    /// Handles a per-device enable/disable toggle. The switch is reverted first so the
    /// confirmation dialog drives the real state change, then re-synced from the device.
    /// </summary>
    private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not DeviceViewModel device || _viewModel == null) return;

        bool wasEnabled = device.IsEnabled;
        bool wantEnable = toggle.IsOn;

        if (wantEnable && !wasEnabled)
        {
            toggle.IsOn = wasEnabled; // revert until confirmed
            await _viewModel.EnableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
        else if (!wantEnable && wasEnabled)
        {
            toggle.IsOn = wasEnabled; // revert until confirmed
            await _viewModel.DisableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
    }

    /// <summary>
    /// Builds the right-click context menu for a device: details, copy device ID, and a
    /// toggle entry whose label reflects the action that will be performed.
    /// </summary>
    private void Device_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not DeviceViewModel device) return;
        if (_viewModel == null) return;

        var flyout = new MenuFlyout();

        var detailsItem = new MenuFlyoutItem { Text = _loc["DeviceDetails"] };
        detailsItem.Click += (_, _) =>
        {
            Frame.Navigate(typeof(DeviceDetailPage), (device, _viewModel));
        };

        var copyIdItem = new MenuFlyoutItem { Text = _loc["CopyDeviceId"] };
        copyIdItem.Click += (_, _) =>
        {
            var pkg = new DataPackage();
            pkg.SetText(device.DeviceId);
            Clipboard.SetContent(pkg);
            NotificationService.Instance.ShowToast(_loc["Copied"], device.DeviceId);
        };

        var toggleItem = new MenuFlyoutItem
        {
            Text = device.IsEnabled ? _loc["DisableAction"] : _loc["EnableAction"]
        };
        toggleItem.Click += async (_, _) =>
        {
            if (device.IsEnabled)
                await _viewModel.DisableDeviceWithConfirmAsync(device, this);
            else
                await _viewModel.EnableDeviceWithConfirmAsync(device, this);
        };

        flyout.Items.Add(detailsItem);
        flyout.Items.Add(copyIdItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(toggleItem);

        flyout.ShowAt(border, e.GetPosition(border));
    }

    /// <summary>
    /// Enables every disabled device of the given type. All but the last are toggled
    /// directly; the last one goes through the confirmation dialog so a single
    /// confirmation covers the whole batch.
    /// </summary>
    private async void BtnEnableAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not DeviceType type) return;
        if (_viewModel == null) return;

        var batch = _viewModel.AllDevices.Where(d => d.Type == type && !d.IsEnabled).ToList();
        if (batch.Count == 0) return;

        var enabledCount = 0;
        for (int i = 0; i < batch.Count; i++)
        {
            var device = batch[i];
            if (i == batch.Count - 1)
            {
                await _viewModel.EnableDeviceWithConfirmAsync(device, this);
                if (device.IsEnabled) enabledCount++;
            }
            else if (_viewModel.ToggleDevice(device.DeviceId, true))
            {
                device.IsEnabled = true;
                enabledCount++;
            }
        }

        if (enabledCount > 0)
            NotificationService.Instance.ShowToast(_loc["DeviceEnabled"], string.Format(_loc["DevicesEnabledCount"], enabledCount));
    }

    /// <summary>
    /// Disables every enabled device of the given type. The whole batch shares
    /// one confirmation and one auto-restore countdown, so disabling the device
    /// type currently in use always recovers automatically.
    /// </summary>
    private async void BtnDisableAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not DeviceType type) return;
        if (_viewModel == null) return;

        var batch = _viewModel.AllDevices.Where(d => d.Type == type && d.IsEnabled).ToList();
        if (batch.Count == 0) return;

        await _viewModel.DisableDevicesWithConfirmAsync(batch, this);
    }
}
