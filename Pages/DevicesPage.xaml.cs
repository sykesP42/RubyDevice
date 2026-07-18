using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using RubyDevice.Core;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

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
        TextActivityHighlight.Text = _loc["ActivityHighlight"];
        TextStatusAll.Text = _loc["All"];
        TextStatusEnabled.Text = _loc["Enabled"];
        TextStatusDisabled.Text = _loc["Disabled"];
        SortByName.Text = _loc["SortByName"];
        SortByType.Text = _loc["SortByType"];
        SortByStatus.Text = _loc["SortByStatus"];
        SearchBox.PlaceholderText = _loc["SearchPlaceholder"];
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

    private void Device_Click(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is DeviceViewModel device && _viewModel != null)
        {
            Frame.Navigate(typeof(DeviceDetailPage), (device, _viewModel));
        }
    }

    private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.DataContext is not DeviceViewModel device || _viewModel == null) return;

        // 记住当前状态
        bool wasEnabled = device.IsEnabled;
        bool wantEnable = toggle.IsOn;

        if (wantEnable && !wasEnabled)
        {
            // 启用设备：简单确认
            toggle.IsOn = wasEnabled; // 先恢复
            await _viewModel.EnableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
        else if (!wantEnable && wasEnabled)
        {
            // 禁用设备：5秒倒计时 + 10秒自动恢复
            toggle.IsOn = wasEnabled; // 先恢复
            await _viewModel.DisableDeviceWithConfirmAsync(device, this);
            toggle.IsOn = device.IsEnabled;
        }
    }

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
        };

        var toggleText = device.IsEnabled ? _loc["Disabled"] : _loc["Enabled"];
        var toggleItem = new MenuFlyoutItem { Text = toggleText };
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

    private async void BtnEnableAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not DeviceType type) return;
        if (_viewModel == null) return;

        var batch = _viewModel.AllDevices.Where(d => d.Type == type && !d.IsEnabled).ToList();
        if (batch.Count == 0) return;

        for (int i = 0; i < batch.Count; i++)
        {
            var device = batch[i];
            if (i == batch.Count - 1)
                await _viewModel.EnableDeviceWithConfirmAsync(device, this);
            else if (_viewModel.ToggleDevice(device.DeviceId, true))
                device.IsEnabled = true;
        }
    }

    private async void BtnDisableAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not DeviceType type) return;
        if (_viewModel == null) return;

        var batch = _viewModel.AllDevices.Where(d => d.Type == type && d.IsEnabled).ToList();
        if (batch.Count == 0) return;

        var first = batch.First();
        await _viewModel.DisableDeviceWithConfirmAsync(first, this);
        if (first.IsEnabled) return;

        for (int i = 1; i < batch.Count; i++)
        {
            if (_viewModel.ToggleDevice(batch[i].DeviceId, false))
                batch[i].IsEnabled = false;
        }
    }
}