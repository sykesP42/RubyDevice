using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Windows.Storage.Pickers;
using RubyDevice.Core;
using RubyDevice.Models;
using RubyDevice.Services;
using static RubyDevice.Helpers.TimeHelper;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class UsageRecordPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private int _selectedDays = 30;
    private string _selectedRangeMode = "days"; // "days", "week", "month"
    private string? _selectedDeviceId;
    private bool _isLoaded;

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
        if (!_isLoaded) return;

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
        RangeWeekly.Content = _loc["ByWeek"];
        RangeMonthly.Content = _loc["ByMonth"];

        LegendActive.Text = _loc["ActiveTime"];
        LegendEnabled.Text = _loc["EnabledTime"];
        TextExport.Text = _loc["ExportData"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        _isLoaded = true;

        // Subscribe to canvas size changed to redraw chart
        ChartCanvas.SizeChanged += ChartCanvas_SizeChanged;

        UpdateDeviceList();
        UpdateStats();
        UpdateHistory();

        UsageTrackingService.Instance.TrackingChanged += OnTrackingChanged;

        // Set retention days
        RetentionBox.Value = UsageTrackingService.Instance.Config.RetentionDays;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isLoaded = false;
        ChartCanvas.SizeChanged -= ChartCanvas_SizeChanged;
        UsageTrackingService.Instance.TrackingChanged -= OnTrackingChanged;
        _loc.PropertyChanged -= OnLocalizationChanged;
        base.OnNavigatedFrom(e);
    }

    private void ChartCanvas_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (!_isLoaded) return;
        LoadHistoryData();
    }

    private void OnTrackingChanged(object? sender, EventArgs e)
    {
        if (!_isLoaded) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isLoaded) return;
            UpdateDeviceList();
            UpdateStats();
        });
    }

    private void UpdateDeviceList()
    {
        if (_viewModel == null || !_isLoaded) return;

        DeviceList.ItemsSource = _viewModel.AllDevices;

        // Check if any device is being tracked
        var hasTracking = _viewModel.AllDevices.Any(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        TextNoTracking.Visibility = hasTracking ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateStats()
    {
        if (_viewModel == null || !_isLoaded) return;

        var trackedCount = _viewModel.AllDevices.Count(d => UsageTrackingService.Instance.IsTracking(d.DeviceId));
        CountTracked.Text = trackedCount.ToString();

        var (activeSeconds, enabledSeconds) = UsageTrackingService.Instance.GetTodayTotals();
        ValueActiveTime.Text = FormatTime(activeSeconds);
        ValueEnabledTime.Text = FormatTime(enabledSeconds);
    }

    private void UpdateHistory()
    {
        if (_viewModel == null || !_isLoaded) return;

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
        if (string.IsNullOrEmpty(_selectedDeviceId) || !_isLoaded) return;

        if (_selectedRangeMode == "week")
        {
            LoadWeeklyData();
        }
        else if (_selectedRangeMode == "month")
        {
            LoadMonthlyData();
        }
        else
        {
            LoadDailyData();
        }
    }

    private void LoadDailyData()
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
        DrawDailyChart(history);
    }

    private void LoadWeeklyData()
    {
        if (string.IsNullOrEmpty(_selectedDeviceId)) return;

        var weeklyData = UsageTrackingService.Instance.GetWeeklyUsage(_selectedDeviceId, 12);

        var displayItems = weeklyData.Select(w => new
        {
            DateText = w.WeekLabel,
            ActiveText = FormatTime(w.TotalActiveSeconds),
            EnabledText = FormatTime(w.TotalEnabledSeconds)
        }).ToList();

        HistoryList.ItemsSource = displayItems;
        DrawWeeklyChart(weeklyData);
    }

    private void LoadMonthlyData()
    {
        if (string.IsNullOrEmpty(_selectedDeviceId)) return;

        var monthlyData = UsageTrackingService.Instance.GetMonthlyUsage(_selectedDeviceId, 12);

        var displayItems = monthlyData.Select(m => new
        {
            DateText = m.MonthLabel,
            ActiveText = FormatTime(m.TotalActiveSeconds),
            EnabledText = FormatTime(m.TotalEnabledSeconds)
        }).ToList();

        HistoryList.ItemsSource = displayItems;
        DrawMonthlyChart(monthlyData);
    }

    private void DrawDailyChart(List<DeviceUsageRecord> records)
    {
        if (!_isLoaded) return;

        ChartCanvas.Children.Clear();
        XAxisLabels.Children.Clear();

        if (records.Count == 0) return;

        var canvasWidth = ChartCanvas.ActualWidth;
        var canvasHeight = ChartCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Calculate max value for Y-axis
        var maxActive = records.Max(r => Math.Max(r.ActiveSeconds, 1));
        var maxEnabled = records.Max(r => Math.Max(r.EnabledSeconds, 1));
        var maxSeconds = Math.Max(maxActive, maxEnabled);

        // Update Y-axis labels
        UpdateYAxisLabels(maxSeconds);

        // Calculate bar dimensions
        var totalBars = records.Count;
        var groupWidth = Math.Max(16, (canvasWidth - 40) / totalBars);
        var barWidth = Math.Max(4, groupWidth / 2 - 1);
        var maxBarHeight = canvasHeight - 20;

        // Draw bars (oldest first)
        for (int i = 0; i < records.Count; i++)
        {
            var record = records[records.Count - 1 - i];
            var x = 20 + i * groupWidth;

            // Active time bar (blue)
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
            Canvas.SetTop(activeBar, maxBarHeight - activeHeight + 10);
            ChartCanvas.Children.Add(activeBar);

            // Enabled time bar (green)
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
            Canvas.SetTop(enabledBar, maxBarHeight - enabledHeight + 10);
            ChartCanvas.Children.Add(enabledBar);
        }

        // Add X-axis labels (show every Nth date based on count)
        var labelInterval = Math.Max(1, totalBars / 8);
        for (int i = 0; i < totalBars; i += labelInterval)
        {
            var record = records[records.Count - 1 - i];
            var label = new TextBlock
            {
                Text = record.Date.ToString("MM-dd"),
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    (Color)Application.Current.Resources["TextTertiaryColor"]),
                Width = groupWidth * labelInterval,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            XAxisLabels.Children.Add(label);
        }
    }

    private void DrawWeeklyChart(List<WeeklyUsageSummary> weeklyData)
    {
        if (!_isLoaded || weeklyData.Count == 0) return;

        ChartCanvas.Children.Clear();
        XAxisLabels.Children.Clear();

        var canvasWidth = ChartCanvas.ActualWidth;
        var canvasHeight = ChartCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var maxActive = weeklyData.Max(w => Math.Max(w.TotalActiveSeconds, 1));
        var maxEnabled = weeklyData.Max(w => Math.Max(w.TotalEnabledSeconds, 1));
        var maxSeconds = Math.Max(maxActive, maxEnabled);

        UpdateYAxisLabels(maxSeconds);

        var totalBars = weeklyData.Count;
        var groupWidth = Math.Max(20, (canvasWidth - 40) / totalBars);
        var barWidth = Math.Max(6, groupWidth / 2 - 2);
        var maxBarHeight = canvasHeight - 20;

        for (int i = 0; i < weeklyData.Count; i++)
        {
            var week = weeklyData[i];
            var x = 20 + i * groupWidth;

            // Active bar
            var activeHeight = (week.TotalActiveSeconds / maxSeconds) * maxBarHeight;
            var activeBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, activeHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(activeBar, $"{week.WeekLabel}\n{_loc["ActiveTime"]}: {FormatTime(week.TotalActiveSeconds)}");
            Canvas.SetLeft(activeBar, x);
            Canvas.SetTop(activeBar, maxBarHeight - activeHeight + 10);
            ChartCanvas.Children.Add(activeBar);

            // Enabled bar
            var enabledHeight = (week.TotalEnabledSeconds / maxSeconds) * maxBarHeight;
            var enabledBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, enabledHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(enabledBar, $"{week.WeekLabel}\n{_loc["EnabledTime"]}: {FormatTime(week.TotalEnabledSeconds)}");
            Canvas.SetLeft(enabledBar, x + barWidth + 2);
            Canvas.SetTop(enabledBar, maxBarHeight - enabledHeight + 10);
            ChartCanvas.Children.Add(enabledBar);

            // X-axis label
            var label = new TextBlock
            {
                Text = week.WeekStartDate.ToString("MM-dd"),
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    (Color)Application.Current.Resources["TextTertiaryColor"]),
                Width = groupWidth,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            XAxisLabels.Children.Add(label);
        }
    }

    private void DrawMonthlyChart(List<MonthlyUsageSummary> monthlyData)
    {
        if (!_isLoaded || monthlyData.Count == 0) return;

        ChartCanvas.Children.Clear();
        XAxisLabels.Children.Clear();

        var canvasWidth = ChartCanvas.ActualWidth;
        var canvasHeight = ChartCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        var maxActive = monthlyData.Max(m => Math.Max(m.TotalActiveSeconds, 1));
        var maxEnabled = monthlyData.Max(m => Math.Max(m.TotalEnabledSeconds, 1));
        var maxSeconds = Math.Max(maxActive, maxEnabled);

        UpdateYAxisLabels(maxSeconds);

        var totalBars = monthlyData.Count;
        var groupWidth = Math.Max(30, (canvasWidth - 40) / totalBars);
        var barWidth = Math.Max(10, groupWidth / 2 - 3);
        var maxBarHeight = canvasHeight - 20;

        for (int i = 0; i < monthlyData.Count; i++)
        {
            var month = monthlyData[i];
            var x = 20 + i * groupWidth;

            // Active bar
            var activeHeight = (month.TotalActiveSeconds / maxSeconds) * maxBarHeight;
            var activeBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, activeHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(activeBar, $"{month.MonthLabel}\n{_loc["ActiveTime"]}: {FormatTime(month.TotalActiveSeconds)}");
            Canvas.SetLeft(activeBar, x);
            Canvas.SetTop(activeBar, maxBarHeight - activeHeight + 10);
            ChartCanvas.Children.Add(activeBar);

            // Enabled bar
            var enabledHeight = (month.TotalEnabledSeconds / maxSeconds) * maxBarHeight;
            var enabledBar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, enabledHeight),
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
                RadiusX = 2,
                RadiusY = 2
            };
            ToolTipService.SetToolTip(enabledBar, $"{month.MonthLabel}\n{_loc["EnabledTime"]}: {FormatTime(month.TotalEnabledSeconds)}");
            Canvas.SetLeft(enabledBar, x + barWidth + 3);
            Canvas.SetTop(enabledBar, maxBarHeight - enabledHeight + 10);
            ChartCanvas.Children.Add(enabledBar);

            // X-axis label
            var label = new TextBlock
            {
                Text = month.MonthLabel,
                FontSize = 10,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    (Color)Application.Current.Resources["TextTertiaryColor"]),
                Width = groupWidth,
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
            if (tag == "week")
            {
                _selectedRangeMode = "week";
            }
            else if (tag == "month")
            {
                _selectedRangeMode = "month";
            }
            else
            {
                _selectedRangeMode = "days";
                _selectedDays = int.Parse(tag);
            }
            LoadHistoryData();
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || string.IsNullOrEmpty(_selectedDeviceId)) return;

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = $"usage_export_{DateTime.Now:yyyyMMdd_HHmmss}";
        picker.FileTypeChoices.Add("CSV (Comma-separated)", new List<string> { ".csv" });

        // Get window handle for the picker
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            string csvContent;

            if (_selectedRangeMode == "week")
            {
                var weeklyData = UsageTrackingService.Instance.GetWeeklyUsage(_selectedDeviceId, 12);
                csvContent = DataExportService.GenerateWeeklyCsvContent(weeklyData, _viewModel.AllDevices.ToList());
            }
            else if (_selectedRangeMode == "month")
            {
                var monthlyData = UsageTrackingService.Instance.GetMonthlyUsage(_selectedDeviceId, 12);
                csvContent = DataExportService.GenerateMonthlyCsvContent(monthlyData, _viewModel.AllDevices.ToList());
            }
            else
            {
                var days = _selectedDays == 0 ? 365 : _selectedDays;
                var records = UsageTrackingService.Instance.GetUsageHistory(_selectedDeviceId, days);
                csvContent = DataExportService.GenerateCsvContent(records, _viewModel.AllDevices.ToList());
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, csvContent);

            // Show success notification
            var notifyService = NotificationService.Instance;
            notifyService.ShowToast(_loc["ExportSuccess"], file.Path);
        }
        catch (Exception ex)
        {
            var notifyService = NotificationService.Instance;
            notifyService.ShowToast(_loc["ExportFailed"], ex.Message);
        }
    }
}
