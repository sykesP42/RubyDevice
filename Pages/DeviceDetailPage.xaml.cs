using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Core;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class DeviceDetailPage : Page
{
    private DeviceViewModel? _device;
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _isToggling = false;

    public DeviceDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is (DeviceViewModel device, MainViewModel vm))
        {
            _device = device;
            _viewModel = vm;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (_device == null) return;

        // Icon
        DeviceIcon.Text = _device.Type switch
        {
            DeviceType.Keyboard => "",
            DeviceType.Mouse => "",
            DeviceType.Touchpad => "",
            _ => ""  // Question mark device
        };

        DeviceNameText.Text = _device.Name;
        DeviceTypeText.Text = _device.TypeName;
        DeviceStatus.Text = _device.StatusText;
        DeviceToggle.IsOn = _device.IsEnabled;

        // Info
        InfoManufacturer.Text = string.IsNullOrEmpty(_device.Manufacturer) ? "Unknown" : _device.Manufacturer;
        InfoVid.Text = string.IsNullOrEmpty(_device.VendorId) ? "N/A" : _device.VendorId;
        InfoPid.Text = string.IsNullOrEmpty(_device.ProductId) ? "N/A" : _device.ProductId;
        InfoConnection.Text = _device.IsExternal ? "External" : "Built-in";
        InfoDeviceId.Text = _device.DeviceId;

        // Note
        NoteBox.Text = _device.UserNote;

        // Device-specific features placeholder
        FeaturesCard.Visibility = Visibility.Collapsed;
    }

    private async void DeviceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_device == null || _viewModel == null || _isToggling) return;

        _isToggling = true;

        // 记住当前状态
        bool wasEnabled = _device.IsEnabled;
        bool wantEnable = DeviceToggle.IsOn;

        if (wantEnable && !wasEnabled)
        {
            // 启用设备：简单确认
            DeviceToggle.IsOn = wasEnabled; // 先恢复
            await _viewModel.EnableDeviceWithConfirmAsync(_device, this);
            DeviceToggle.IsOn = _device.IsEnabled;
            DeviceStatus.Text = _device.StatusText;
        }
        else if (!wantEnable && wasEnabled)
        {
            // 禁用设备：5秒倒计时 + 10秒自动恢复
            DeviceToggle.IsOn = wasEnabled; // 先恢复
            await _viewModel.DisableDeviceWithConfirmAsync(_device, this);
            DeviceToggle.IsOn = _device.IsEnabled;
            DeviceStatus.Text = _device.StatusText;
        }

        _isToggling = false;
    }

    private void NoteBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_device != null && _viewModel != null)
            _viewModel.SetDeviceNote(_device.DeviceId, NoteBox.Text);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }
}