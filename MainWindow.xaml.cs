using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RubyDevice.Core;
using RubyDevice.Models;
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
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    private readonly TrayIconService _trayIcon = new();
    private bool _exitRequestedFromTray;

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
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        var appWindow = _appWindow;
        RestoreWindowBounds(appWindow);
        appWindow?.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico"));

        // Close-to-tray support: cancel the close and hide instead
        if (appWindow != null)
        {
            appWindow.Closing += OnAppWindowClosing;
        }

        // System tray icon
        _trayIcon.Initialize(System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico"), "RubyDevice");
        _trayIcon.OpenRequested += OnTrayOpenRequested;
        _trayIcon.ExitRequested += OnTrayExitRequested;

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

    /// <summary>
    /// Applies the previously saved window size and position. Invalid or
    /// off-screen values fall back to the default size centered by the OS.
    /// </summary>
    private void RestoreWindowBounds(Microsoft.UI.Windowing.AppWindow? appWindow)
    {
        if (appWindow == null) return;

        var settings = AppSettings.Load();
        if (settings.WindowWidth >= 200 && settings.WindowHeight >= 200)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(settings.WindowWidth, settings.WindowHeight));

            if (IsPointOnVirtualScreen(settings.WindowX, settings.WindowY))
            {
                appWindow.Move(new Windows.Graphics.PointInt32(settings.WindowX, settings.WindowY));
            }

            if (settings.WindowMaximized)
            {
                (appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.Maximize();
            }
        }
        else
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 700));
        }
    }

    /// <summary>
    /// Persists the window's normal (restored) bounds and maximized state so the
    /// next launch reopens where the user left it.
    /// </summary>
    private void SaveWindowBounds()
    {
        if (_appWindow == null) return;

        try
        {
            var placement = NativeMethods.GetWindowPlacement(_hwnd);
            var rect = placement.rcNormalPosition;

            var settings = AppSettings.Load();
            settings.WindowWidth = rect.Right - rect.Left;
            settings.WindowHeight = rect.Bottom - rect.Top;
            settings.WindowX = rect.Left;
            settings.WindowY = rect.Top;
            settings.WindowMaximized = placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED;
            settings.Save();
        }
        catch
        {
            // Best effort; losing window bounds must never block shutdown
        }
    }

    private static bool IsPointOnVirtualScreen(int x, int y)
    {
        if (x == int.MinValue || y == int.MinValue) return false;
        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int cx = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int cy = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        // Allow a small margin so partially off-screen windows still restore
        const int margin = 200;
        return x >= vx - margin && y >= vy - margin && x <= vx + cx + margin && y <= vy + cy + margin;
    }

    /// <summary>
    /// Intercepts the close button when "close to tray" is enabled and hides the
    /// window instead; the app keeps running with its tray icon.
    /// </summary>
    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_exitRequestedFromTray) return;

        var settings = AppSettings.Load();
        if (!settings.CloseToTray) return;

        args.Cancel = true;
        DispatcherQueue.TryEnqueue(() => _appWindow?.Hide());
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _appWindow?.Show();
            Activate();
            NativeMethods.SetForegroundWindow(_hwnd);
        });
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Skip the close-to-tray cancellation for a real exit
            _exitRequestedFromTray = true;
            Close();
        });
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
        _trayIcon.OpenRequested -= OnTrayOpenRequested;
        _trayIcon.ExitRequested -= OnTrayExitRequested;
        _trayIcon.Dispose();
        SaveWindowBounds();
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
