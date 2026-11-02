using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RubyDevice.Core;
using RubyDevice.Pages;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice;

/// <summary>
/// Main application window hosting the navigation frame and header.
/// Handles raw input registration, in-app notifications, and device manager lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private DeviceManager? _deviceManager;
    private IntPtr _hwnd;
    private string? _currentPageTag;
    private readonly DispatcherTimer _notificationTimer;

    /// <summary>
    /// Builds the main window, registers raw input handling, and initializes the
    /// usage tracking service
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        // Force the Fluent theme to match the hand-rolled palette; Default would
        // follow the system and misrender default control colors (e.g. dark mode
        // text on the light palettes).
        RootGrid.RequestedTheme = ThemeService.Instance.CurrentTheme == AppTheme.Dark
            ? ElementTheme.Dark
            : ElementTheme.Light;

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

        ThemeService.Instance.PropertyChanged += (_, _) => UpdateNavIconColors();

        // In-app notifications
        NotificationService.Instance.NotificationPosted += OnNotificationPosted;
        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _notificationTimer.Tick += (_, _) => HideNotification();

        NavList.SelectedIndex = 0;

        Closed += OnClosed;
    }

    private void OnRawInput(IntPtr lParam)
    {
        // Process raw input to track which device generated the input
        _deviceManager?.ProcessRawInput(lParam);
    }

    /// <summary>
    /// Cleans up on window close: saves device data and disposes services
    /// </summary>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        NotificationService.Instance.NotificationPosted -= OnNotificationPosted;
        _viewModel.SaveDeviceData();
        WindowSubclassHelper.UninstallSubclass(_hwnd);
        _deviceManager?.Dispose();
        Services.UsageTrackingService.Instance.Dispose();
    }

    /// <summary>
    /// Displays an in-app notification banner. Safe to call from any thread.
    /// </summary>
    private void OnNotificationPosted(object? sender, AppNotification notification)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            NotificationBar.Title = notification.Title;
            NotificationBar.Message = notification.Message;
            NotificationBar.Severity = notification.Severity switch
            {
                NotificationSeverity.Success => InfoBarSeverity.Success,
                NotificationSeverity.Warning => InfoBarSeverity.Warning,
                _ => InfoBarSeverity.Informational
            };
            NotificationBar.IsOpen = true;

            _notificationTimer.Stop();
            _notificationTimer.Interval = TimeSpan.FromSeconds(Math.Max(2, notification.DurationSeconds));
            _notificationTimer.Start();
        });
    }

    private void HideNotification()
    {
        _notificationTimer.Stop();
        NotificationBar.IsOpen = false;
    }

    private void NotificationBar_CloseClick(InfoBar sender, object args)
    {
        _notificationTimer.Stop();
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
        UpdateNavIconColors();

        if (NavList.SelectedItem is not ListViewItem item) return;

        _currentPageTag = item.Tag?.ToString();
        NavigateToPage(_currentPageTag);
    }

    private void UpdateNavIconColors()
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

        var selected = item.Tag?.ToString();
        if (selected == "Devices") IconDevices.Foreground = primaryBrush;
        else if (selected == "UsageRecord") IconUsageRecord.Foreground = primaryBrush;
        else if (selected == "Statistics") IconStatistics.Foreground = primaryBrush;
        else if (selected == "Timer") IconTimer.Foreground = primaryBrush;
        else if (selected == "Settings") IconSettings.Foreground = primaryBrush;
        else if (selected == "About") IconAbout.Foreground = primaryBrush;
    }

    /// <summary>
    /// Navigates to the page matching the nav tag and updates the header text.
    /// The back stack is cleared so device-detail navigation never leaks across
    /// top-level sections.
    /// </summary>
    private void NavigateToPage(string? tag)
    {
        ContentFrame.BackStack.Clear();

        switch (tag)
        {
            case "Devices":
                ContentFrame.Navigate(typeof(DevicesPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderDevices"];
                HeaderSubtitle.Text = _loc["HeaderDevicesDesc"];
                break;
            case "UsageRecord":
                ContentFrame.Navigate(typeof(Pages.UsageRecordPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderUsageRecord"];
                HeaderSubtitle.Text = _loc["HeaderUsageRecordDesc"];
                break;
            case "Statistics":
                ContentFrame.Navigate(typeof(StatisticsPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderStatistics"];
                HeaderSubtitle.Text = _loc["HeaderStatisticsDesc"];
                break;
            case "Timer":
                ContentFrame.Navigate(typeof(TimerPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderTimer"];
                HeaderSubtitle.Text = _loc["HeaderTimerDesc"];
                break;
            case "Settings":
                ContentFrame.Navigate(typeof(SettingsPage), _viewModel);
                HeaderTitle.Text = _loc["HeaderSettings"];
                HeaderSubtitle.Text = _loc["HeaderSettingsDesc"];
                break;
            case "About":
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
}
