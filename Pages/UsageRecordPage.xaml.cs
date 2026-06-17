using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using RubyDevice.Core;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class UsageRecordPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private int _selectedDays = 30;
    private string? _selectedDeviceId;

    public UsageRecordPage()
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
        TextTrackedDevices.Text = _loc["TrackedDevices"];
        TextNoTracking.Text = _loc["EnableTrackingHint"];
        TextTrackedCount.Text = _loc["TrackedDevices"];
        TextActiveTime.Text = _loc["ActiveTime"];
        TextEnabledTime.Text = _loc["EnabledTime"];
        TextSettings.Text = _loc["Behavior"];
        TextRetentionDays.Text = _loc["RetentionDays"];
        TextUsageHistory.Text = _loc["UsageHistory"];

        Range7Days.Content = _loc["Last7Days"];
        Range30Days.Content = _loc["Last30Days"];
        RangeAll.Content = _loc["AllTime"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        UpdateDeviceList();
        UpdateStats();
        UpdateHistory();

        UsageTrackingService.Instance.TrackingChanged += OnTrackingChanged;

        // Set retention days
        RetentionBox.Value = UsageTrackingService.Instance.Config.RetentionDays;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        UsageTrackingService.Instance.TrackingChanged -= OnTrackingChanged;
        _loc.PropertyChanged -= OnLocalizationChanged;
    }

    private void OnTrackingChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateDeviceList();
            UpdateStats();
        });
    }

    private void UpdateDeviceList()
    {
        if (_viewModel == null) return;

        DeviceList.ItemsSource = _viewModel.AllDevices;

        // Check if any device is being tracked
        var hasTracking = _viewModel.AllDevices.Any(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        TextNoTracking.Visibility = hasTracking ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateStats()
    {
        if (_viewModel == null) return;

        var trackedCount = _viewModel.AllDevices.Count(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        CountTracked.Text = trackedCount.ToString();

        var (activeSeconds, enabledSeconds) = UsageTrackingService.Instance.GetTodayTotals();
        ValueActiveTime.Text = FormatTime(activeSeconds);
        ValueEnabledTime.Text = FormatTime(enabledSeconds);
    }

    private void UpdateHistory()
    {
        if (_viewModel == null) return;

        // Populate device selector with tracked devices
        var trackedDevices = _viewModel.AllDevices
            .Where(d => UsageTrackingService.Instance.IsTracking(d.DeviceId))
            .ToList();

        DeviceSelector.ItemsSource = trackedDevices;
        DeviceSelector.DisplayMemberPath = "Name";

        if (trackedDevices.Count > 0)
        {
            if (_selectedDeviceId == null || !trackedDevices.Any(d => d.DeviceId == _selectedDeviceId))
            {
                DeviceSelector.SelectedIndex = 0;
                _selectedDeviceId = trackedDevices[0].DeviceId;
            }
        }

        LoadHistoryData();
    }

    private void LoadHistoryData()
    {
        if (string.IsNullOrEmpty(_selectedDeviceId)) return;

        var days = _selectedDays == 0 ? 365 : _selectedDays;
        var history = UsageTrackingService.Instance.GetUsageHistory(_selectedDeviceId, days);

        // Update list
        var displayItems = history.Select(r => new
        {
            DateText = r.Date.ToString("yyyy-MM-dd"),
            ActiveText = FormatTime(r.ActiveSeconds),
            EnabledText = FormatTime(r.EnabledSeconds)
        }).ToList();

        HistoryList.ItemsSource = displayItems;

        // Update chart
        DrawChart(history);
    }

    private void DrawChart(List<DeviceUsageRecord> records)
    {
        ChartCanvas.Children.Clear();

        if (records.Count == 0) return;

        var maxSeconds = records.Max(r => Math.Max(r.ActiveSeconds, 1));
        var barWidth = Math.Max(10, (ChartCanvas.ActualWidth - 40) / records.Count - 2);
        var maxBarHeight = ChartCanvas.ActualHeight - 20;

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[records.Count - 1 - i]; // Reverse to show oldest first
            var barHeight = (record.ActiveSeconds / (double)maxSeconds) * maxBarHeight;
            var x = 20 + i * (barWidth + 2);
            var y = maxBarHeight - barHeight;

            var rect = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, barHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y + 10);
            ChartCanvas.Children.Add(rect);
        }
    }

    private static string FormatTime(long seconds)
    {
        var hours = seconds / 3600;
        var mins = (seconds % 3600) / 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private void TrackingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.Tag is string deviceId)
        {
            UsageTrackingService.Instance.SetTracking(deviceId, toggle.IsOn);

            // Update the DeviceViewModel
            var device = _viewModel?.AllDevices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device != null)
            {
                device.IsTracking = toggle.IsOn;
            }
        }
    }

    private void RetentionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (args.NewValue >= 7 && args.NewValue <= 365)
        {
            UsageTrackingService.Instance.SetRetentionDays((int)args.NewValue);
        }
    }

    private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceSelector.SelectedItem is DeviceViewModel device)
        {
            _selectedDeviceId = device.DeviceId;
            LoadHistoryData();
        }
    }

    private void Range_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _selectedDays = int.Parse(tag);
            LoadHistoryData();
        }
    }
}
