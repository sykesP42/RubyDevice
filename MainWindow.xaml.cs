using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RubyDevice.Core;
using RubyDevice.Pages;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _isMaximized;
    private DeviceManager? _deviceManager;
    private IntPtr _hwnd;
    private string? _currentPageTag;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        // Get window handle
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(1100, 700));

        // Init device manager
        _deviceManager = _viewModel.GetDeviceManager();
        _deviceManager?.RegisterRawInput(_hwnd);
        WindowSubclassHelper.InstallSubclass(_hwnd, OnRawInput);

        // Initialize usage tracking service
        if (_deviceManager != null)
        {
            Services.UsageTrackingService.Instance.Initialize(_deviceManager);
        }

        // Init
        _viewModel.Initialize();
        _loc.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();

        ThemeService.Instance.PropertyChanged += (_, _) => UpdateVisuals();

        NavList.SelectedIndex = 0;

        Closed += OnClosed;
    }

    private void OnRawInput(IntPtr lParam)
    {
        // Process raw input to track which device generated the input
        _deviceManager?.ProcessRawInput(lParam);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _viewModel.SaveDeviceData();
        WindowSubclassHelper.UninstallSubclass(_hwnd);
        _deviceManager?.Dispose();
        Services.UsageTrackingService.Instance.Dispose();
    }

    private void UpdateVisuals()
    {
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        TextDevices.Text = _loc["NavDevices"];
        TextUsageRecord.Text = _loc["NavUsageRecord"];
        TextStatistics.Text = _loc["NavStatistics"];
        TextTimer.Text = _loc["NavTimer"];
        TextSettings.Text = _loc["NavSettings"];
        TextAbout.Text = _loc["NavAbout"];

        // Refresh header and current page when language changes
        if (!string.IsNullOrEmpty(_currentPageTag))
        {
            RefreshCurrentPage();
        }
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var secondaryBrush = (SolidColorBrush)App.Current.Resources["TextSecondaryBrush"];
        var primaryBrush = (SolidColorBrush)App.Current.Resources["PrimaryBrush"];

        IconDevices.Foreground = secondaryBrush;
        IconUsageRecord.Foreground = secondaryBrush;
        IconStatistics.Foreground = secondaryBrush;
        IconTimer.Foreground = secondaryBrush;
        IconSettings.Foreground = secondaryBrush;
        IconAbout.Foreground = secondaryBrush;

        if (NavList.SelectedItem is not ListViewItem item) return;

        _currentPageTag = item.Tag?.ToString();
        NavigateToPage(_currentPageTag);
    }

    private void NavigateToPage(string? tag)
    {
        var primaryBrush = (SolidColorBrush)App.Current.Resources["PrimaryBrush"];

        switch (tag)
        {
            case "Devices":
                IconDevices.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(DevicesPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderDevices"];
                HeaderSubtitle.Text = _loc["HeaderDevicesDesc"];
                break;
            case "UsageRecord":
                IconUsageRecord.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(Pages.UsageRecordPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderUsageRecord"];
                HeaderSubtitle.Text = _loc["HeaderUsageRecordDesc"];
                break;
            case "Statistics":
                IconStatistics.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(StatisticsPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderStatistics"];
                HeaderSubtitle.Text = _loc["HeaderStatisticsDesc"];
                break;
            case "Timer":
                IconTimer.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(TimerPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderTimer"];
                HeaderSubtitle.Text = _loc["HeaderTimerDesc"];
                break;
            case "Settings":
                IconSettings.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(SettingsPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderSettings"];
                HeaderSubtitle.Text = _loc["HeaderSettingsDesc"];
                break;
            case "About":
                IconAbout.Foreground = primaryBrush;
                ContentFrame.Navigate(typeof(AboutPage));
                HeaderTitle.Text = _loc["HeaderAbout"];
                HeaderSubtitle.Text = _loc["HeaderAboutDesc"];
                break;
        }
    }

    private void RefreshCurrentPage()
    {
        NavigateToPage(_currentPageTag);
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        (appWindow?.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.Minimize();
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        var presenter = appWindow?.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter == null) return;

        if (_isMaximized) { presenter.Restore(); _isMaximized = false; }
        else { presenter.Maximize(); _isMaximized = true; }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}