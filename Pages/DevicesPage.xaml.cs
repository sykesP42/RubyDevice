using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
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

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Refresh();
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
}