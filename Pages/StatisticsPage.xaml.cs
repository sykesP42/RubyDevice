using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Core;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class StatisticsPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public MainViewModel? ViewModel => _viewModel;
    private bool _isLoaded = false;

    public StatisticsPage()
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

        TextTotal.Text = _loc["TotalDevices"];
        TextActive.Text = _loc["ActiveDevices"];
        TextDisabled.Text = _loc["DisabledDevices"];
        TextExternal.Text = _loc["ExternalDevices"];
        TextDistribution.Text = _loc["DeviceDistribution"];
        TextKeyboard.Text = _loc["Keyboard"];
        TextMouse.Text = _loc["Mouse"];
        TextTouchpad.Text = _loc["Touchpad"];
        TextTodayUsage.Text = _loc["TodayUsage"];
        TextActiveTime.Text = _loc["ActiveTime"];
        TextEnabledTime.Text = _loc["EnabledTime"];
        TextActivityRate.Text = _loc["ActivityRate"];
        TextRefresh.Text = _loc["Refresh"];
        TextDeviceList.Text = _loc["Devices"];
        TextTrackedCount.Text = _loc["TrackedDevices"] + ":";
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        _isLoaded = true;
        Bindings?.Update();
        UpdateStats();

        if (_viewModel != null)
            _viewModel.PropertyChanged += (_, _) => UpdateStats();

        UsageTrackingService.Instance.TrackingChanged += OnTrackingChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isLoaded = false;
        UsageTrackingService.Instance.TrackingChanged -= OnTrackingChanged;
        _loc.PropertyChanged -= OnLocalizationChanged;
        base.OnNavigatedFrom(e);
    }

    private void OnTrackingChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateStats);
    }

    private void UpdateStats()
    {
        if (_viewModel == null || !_isLoaded) return;
        var devices = _viewModel.AllDevices;

        // Basic counts
        CountTotal.Text = devices.Count.ToString();
        CountActive.Text = devices.Count(d => d.IsEnabled).ToString();
        CountDisabled.Text = devices.Count(d => !d.IsEnabled).ToString();
        CountExternal.Text = devices.Count(d => d.IsExternal).ToString();

        // Type distribution
        var total = devices.Count > 0 ? devices.Count : 1;
        var keyboards = devices.Count(d => d.Type == DeviceType.Keyboard);
        var mice = devices.Count(d => d.Type == DeviceType.Mouse);
        var touchpads = devices.Count(d => d.Type == DeviceType.Touchpad);

        double maxWidth = 180;
        BarKeyboard.Width = (keyboards * maxWidth) / total;
        BarMouse.Width = (mice * maxWidth) / total;
        BarTouchpad.Width = (touchpads * maxWidth) / total;

        CountKeyboard.Text = keyboards.ToString();
        CountMouse.Text = mice.ToString();
        CountTouchpad.Text = touchpads.ToString();

        PctKeyboard.Text = $"({keyboards * 100 / total}%)";
        PctMouse.Text = $"({mice * 100 / total}%)";
        PctTouchpad.Text = $"({touchpads * 100 / total}%)";

        // Today's usage from tracking service
        var (activeSeconds, enabledSeconds) = UsageTrackingService.Instance.GetTodayTotals();
        ValueActiveTime.Text = FormatTime(activeSeconds);
        ValueEnabledTime.Text = FormatTime(enabledSeconds);

        // Activity rate (Active / Enabled)
        var rate = enabledSeconds > 0 ? (activeSeconds / enabledSeconds) * 100 : 0;
        ValueActivityRate.Text = $"{Math.Round(rate)}%";

        // Tracked devices count
        var trackedCount = devices.Count(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        CountTracked.Text = trackedCount.ToString();

        // Device list - sort by name
        var sortedDevices = devices.OrderBy(d => d.Name).ToList();
        UsageList.ItemsSource = sortedDevices;
    }

    private static string FormatTime(double seconds)
    {
        var totalSeconds = (long)seconds;
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Refresh();
        UpdateStats();
    }
}