using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class SettingsPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _initialized;

    // Settings file path
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RubyDevice", "app_settings.json");

    public SettingsPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();
        Loaded += (_, _) =>
        {
            _initialized = true;
            LoadSettings();
        };
    }

    private void UpdateTexts()
    {
        // Appearance
        TextAppearanceTitle.Text = _loc["Appearance"];
        TextAppearanceDesc.Text = _loc["Appearance"];
        TextLanguageLabel.Text = _loc["Language"];
        TextThemeLabel.Text = _loc["Theme"];
        TextThemeLight.Text = _loc["ThemeLight"];
        TextThemeDark.Text = _loc["ThemeDark"];
        TextThemeOcean.Text = _loc["ThemeOcean"];
        TextThemeForest.Text = _loc["ThemeForest"];
        TextThemeSunset.Text = _loc["ThemeSunset"];

        // Behavior
        TextBehaviorTitle.Text = _loc["Behavior"];
        TextBehaviorDesc.Text = _loc["Behavior"];
        TextAutoStart.Text = _loc["AutoStart"];
        TextAutoRefresh.Text = _loc["AutoRefresh"];
        TextShowNotifications.Text = _loc["ShowNotifications"];

        // System Tray
        TextTrayTitle.Text = _loc["SystemTray"];
        TextTrayDesc.Text = _loc["SystemTray"];
        TextMinimizeToTray.Text = _loc["MinimizeToTray"];
        TextShowDeviceCount.Text = _loc["ShowDeviceCount"];
        TextCloseToTray.Text = _loc["CloseToTray"];

        // Data Management
        TextDataTitle.Text = _loc["DataManagement"];
        TextDataDesc.Text = _loc["DataManagement"];
        TextClearData.Text = _loc["ClearUsageData"];

        // About
        TextAboutTitle.Text = _loc["HeaderAbout"];
        TextAboutDesc.Text = _loc["HeaderAboutDesc"];
        TextAboutDescText.Text = _loc["AppDescription"];
        TextVersion.Text = $"{_loc["Version"]}: {_loc["AppVersion"]}";
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    ApplySettings(settings);
                    return settings;
                }
            }
        }
        catch { }

        var defaults = new AppSettings();
        ApplySettings(defaults);
        return defaults;
    }

    private void ApplySettings(AppSettings settings)
    {
        AutoStartToggle.IsOn = settings.AutoStart;
        AutoRefreshToggle.IsOn = settings.AutoRefresh;
        NotificationsToggle.IsOn = settings.ShowNotifications;
        MinimizeToTrayToggle.IsOn = settings.MinimizeToTray;
        ShowDeviceCountToggle.IsOn = settings.ShowDeviceCount;
        CloseToTrayToggle.IsOn = settings.CloseToTray;
    }

    private void SaveSetting<T>(string key, T value)
    {
        var settings = AppSettings.Load();
        settings.Set(key, value);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;

        if (_initialized)
        {
            LoadSettings();
        }

        LanguageRadio.SelectedIndex = _loc.CurrentLanguage == AppLanguage.English ? 0 : 1;
        ThemeGrid.SelectedIndex = (int)ThemeService.Instance.CurrentTheme;
    }

    private void LanguageRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        _loc.CurrentLanguage = LanguageRadio.SelectedIndex == 0 ? AppLanguage.English : AppLanguage.Chinese;
    }

    private void ThemeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeGrid.SelectedIndex < 0) return;
        var theme = (AppTheme)ThemeGrid.SelectedIndex;

        // Update ThemeService
        ThemeService.Instance.CurrentTheme = theme;

        // Apply theme to resources
        var merged = App.Current.Resources.MergedDictionaries;
        while (merged.Count > 1)
            merged.RemoveAt(merged.Count - 1);

        var themeDict = new ResourceDictionary
        {
            Source = theme switch
            {
                AppTheme.Dark => new Uri("ms-appx:///Themes/DarkTheme.xaml"),
                AppTheme.Ocean => new Uri("ms-appx:///Themes/OceanTheme.xaml"),
                AppTheme.Forest => new Uri("ms-appx:///Themes/ForestTheme.xaml"),
                AppTheme.Sunset => new Uri("ms-appx:///Themes/SunsetTheme.xaml"),
                _ => new Uri("ms-appx:///Themes/LightTheme.xaml")
            }
        };
        merged.Add(themeDict);
    }

    private void AutoStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.AutoStart), AutoStartToggle.IsOn);

        // TODO: Implement actual auto-start via Windows registry
        // (requires StartupTask or registry key)
    }

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.AutoRefresh), AutoRefreshToggle.IsOn);

        // TODO: Implement timer-based auto-refresh
    }

    private void Notifications_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.ShowNotifications), NotificationsToggle.IsOn);
    }

    private void MinimizeToTray_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.MinimizeToTray), MinimizeToTrayToggle.IsOn);

        // TODO: Implement minimize to system tray
    }

    private void ShowDeviceCount_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.ShowDeviceCount), ShowDeviceCountToggle.IsOn);

        // TODO: Update tray icon tooltip with device count
    }

    private void CloseToTray_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSetting(nameof(AppSettings.CloseToTray), CloseToTrayToggle.IsOn);

        // TODO: Override close button behavior
    }

    private async void BtnClearData_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = _loc["DataManagement"],
            Content = _loc["ClearDataConfirm"],
            PrimaryButtonText = _loc["Save"],
            CloseButtonText = _loc["Cancel"],
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // Clear UsageTrackingService data
            var trackingService = UsageTrackingService.Instance;

            // Clear the tracking settings file
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RubyDevice");

            var usageDataPath = Path.Combine(appDataPath, "usage_data.json");
            var trackingSettingsPath = Path.Combine(appDataPath, "tracking_settings.json");

            try
            {
                if (File.Exists(usageDataPath))
                    File.Delete(usageDataPath);
                if (File.Exists(trackingSettingsPath))
                    File.Delete(trackingSettingsPath);
            }
            catch { }

            // Refresh the app
            _viewModel?.Refresh();

            NotificationService.Instance.ShowToast(_loc["DataCleared"], "");
        }
    }
}
