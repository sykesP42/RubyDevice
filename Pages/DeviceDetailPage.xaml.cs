using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using RubyDevice.Core;
using static RubyDevice.Helpers.TimeHelper;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class DeviceDetailPage : Page
{
    private DeviceViewModel? _device;
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _isToggling = false;
    private bool _isLoaded = false;
    private int _selectedDays = 7;

    public DeviceDetailPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += OnLocalizationChanged;
        UpdateTexts();
    }

    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateTexts);
    }

    private void UpdateTexts()
    {
        if (!_isLoaded) return;

        TextBack.Text = _loc["Back"];
        TextDeviceInfo.Text = _loc["DeviceInfo"];
        TextManufacturer.Text = _loc["Manufacturer"];
        TextVid.Text = _loc["VendorId"];
        TextPid.Text = _loc["ProductId"];
        TextConnection.Text = _loc["Connection"];
        TextDeviceId.Text = _loc["DeviceId"];
        TextUsageStats.Text = _loc["UsageStats"];
        TextActiveTime.Text = _loc["ActiveTime"];
        TextEnabledTime.Text = _loc["EnabledTime"];
        TextTracking.Text = _loc["TrackUsage"];
        TextHistory.Text = _loc["UsageHistory"];
        TextNotes.Text = _loc["Notes"];
        Range7Days.Content = _loc["Last7Days"];
        Range30Days.Content = _loc["Last30Days"];
        NoteBox.PlaceholderText = _loc["AddNoteHint"];
        LegendActive.Text = _loc["ActiveTime"];
        LegendEnabled.Text = _loc["EnabledTime"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is (DeviceViewModel device, MainViewModel vm))
        {
            _device = device;
            _viewModel = vm;
            _isLoaded = true;
            UpdateUI();
            LoadUsageData();
        }

        ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isLoaded = false;
        ChartCanvas.SizeChanged -= ChartCanvas_SizeChanged;
        _loc.PropertyChanged -= OnLocalizationChanged;
        base.OnNavigatedFrom(e);
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isLoaded) DrawChart();
    }

    private void UpdateUI()
    {
        if (_device == null) return;

        DeviceIcon.Text = _device.Type switch
        {
            DeviceType.Keyboard => "",
            DeviceType.Mouse => "",
            DeviceType.Touchpad => "",
            _ => ""
        };

        DeviceNameText.Text = _device.Name;
        DeviceTypeText.Text = _device.TypeName;
        DeviceStatus.Text = _device.StatusText;
        DeviceToggle.IsOn = _device.IsEnabled;

        InfoManufacturer.Text = string.IsNullOrEmpty(_device.Manufacturer) ? _loc["Unknown"] : _device.Manufacturer;
        InfoVid.Text = string.IsNullOrEmpty(_device.VendorId) ? "N/A" : _device.VendorId;
        InfoPid.Text = string.IsNullOrEmpty(_device.ProductId) ? "N/A" : _device.ProductId;
        InfoConnection.Text = _device.IsExternal ? _loc["External"] : _loc["BuiltIn"];
        InfoDeviceId.Text = _device.DeviceId;

        NoteBox.Text = _device.UserNote;

        TrackingToggle.IsOn = UsageTrackingService.Instance.IsTracking(_device.DeviceId);
    }

    private void LoadUsageData()
    {
        if (_device == null) return;

        var (activeSeconds, enabledSeconds) = UsageTrackingService.Instance.GetTodayTotals();
        ValueActiveTime.Text = FormatTime(activeSeconds);
        ValueEnabledTime.Text = FormatTime(enabledSeconds);

        DrawChart();
    }

    private void DrawChart()
    {
        if (_device == null || !_isLoaded) return;

        ChartCanvas.Children.Clear();
        XAxisLabels.Children.Clear();

        var history = UsageTrackingService.Instance.GetUsageHistory(_device.DeviceId, _selectedDays);
        if (history.Count == 0) return;

        var canvasWidth = ChartCanvas.ActualWidth;
        var canvasHeight = ChartCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Calculate max for both active and enabled
        var maxActive = history.Max(r => Math.Max(r.ActiveSeconds, 1));
        var maxEnabled = history.Max(r => Math.Max(r.EnabledSeconds, 1));
        var maxSeconds = Math.Max(maxActive, maxEnabled);

        // Update Y-axis labels
        UpdateYAxisLabels(maxSeconds);

        var totalBars = history.Count;
        var groupWidth = Math.Max(14, (canvasWidth - 20) / totalBars);
        var barWidth = Math.Max(4, groupWidth / 2 - 1);
        var maxBarHeight = canvasHeight - 10;

        for (int i = 0; i < history.Count; i++)
        {
            var record = history[history.Count - 1 - i];
            var x = 10 + i * groupWidth;

            // Active bar (blue)
            var activeHeight = (record.ActiveSeconds / maxSeconds) * maxBarHeight;
            var activeBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, activeHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(activeBar, $"{record.Date:MM-dd}\n{_loc["ActiveTime"]}: {FormatTime(record.ActiveSeconds)}");
            Canvas.SetLeft(activeBar, x);
            Canvas.SetTop(activeBar, maxBarHeight - activeHeight + 5);
            ChartCanvas.Children.Add(activeBar);

            // Enabled bar (green)
            var enabledHeight = (record.EnabledSeconds / maxSeconds) * maxBarHeight;
            var enabledBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, enabledHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(enabledBar, $"{record.Date:MM-dd}\n{_loc["EnabledTime"]}: {FormatTime(record.EnabledSeconds)}");
            Canvas.SetLeft(enabledBar, x + barWidth + 1);
            Canvas.SetTop(enabledBar, maxBarHeight - enabledHeight + 5);
            ChartCanvas.Children.Add(enabledBar);
        }

        // Add X-axis labels
        var labelInterval = Math.Max(1, totalBars / 6);
        for (int i = 0; i < totalBars; i += labelInterval)
        {
            var record = history[history.Count - 1 - i];
            var label = new TextBlock
            {
                Text = record.Date.ToString("MM-dd"),
                FontSize = 9,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    (Color)Application.Current.Resources["TextTertiaryColor"]),
                Width = groupWidth * labelInterval,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            XAxisLabels.Children.Add(label);
        }
    }

    private void UpdateYAxisLabels(double maxSeconds)
    {
        var maxHours = maxSeconds / 3600;
        var midHours = maxHours / 2;

        YAxisMax.Text = maxHours >= 1 ? $"{Math.Round(maxHours, 1)}h" : $"{Math.Round(maxSeconds / 60)}m";
        YAxisMid.Text = midHours >= 1 ? $"{Math.Round(midHours, 1)}h" : $"{Math.Round(midHours * 60)}m";
        YAxisMin.Text = "0";
    }

    private async void DeviceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_device == null || _viewModel == null || _isToggling) return;

        _isToggling = true;

        bool wasEnabled = _device.IsEnabled;
        bool wantEnable = DeviceToggle.IsOn;

        if (wantEnable && !wasEnabled)
        {
            DeviceToggle.IsOn = wasEnabled;
            await _viewModel.EnableDeviceWithConfirmAsync(_device, this);
            DeviceToggle.IsOn = _device.IsEnabled;
            DeviceStatus.Text = _device.StatusText;
        }
        else if (!wantEnable && wasEnabled)
        {
            DeviceToggle.IsOn = wasEnabled;
            await _viewModel.DisableDeviceWithConfirmAsync(_device, this);
            DeviceToggle.IsOn = _device.IsEnabled;
            DeviceStatus.Text = _device.StatusText;
        }

        _isToggling = false;
    }

    private void TrackingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_device == null) return;

        UsageTrackingService.Instance.SetTracking(_device.DeviceId, TrackingToggle.IsOn);
        _device.IsTracking = TrackingToggle.IsOn;
    }

    private void NoteBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_device != null && _viewModel != null)
            _viewModel.SetDeviceNote(_device.DeviceId, NoteBox.Text);
    }

    private void Range_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _selectedDays = int.Parse(tag);
            DrawChart();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }
}