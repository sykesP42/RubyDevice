using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Models;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

/// <summary>
/// Application settings page: appearance (language/theme), behavior
/// (auto-start, auto-refresh, notifications), data management, and about.
/// Settings persist to %AppData%\RubyDevice\app_settings.json.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _initialized;

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
        TextAppearanceDesc.Text = _loc["AppearanceDesc"];
        TextLanguageLabel.Text = _loc["Language"];
        TextThemeLabel.Text = _loc["Theme"];
        TextThemeLight.Text = _loc["ThemeLight"];
        TextThemeDark.Text = _loc["ThemeDark"];
        TextThemeOcean.Text = _loc["ThemeOcean"];
        TextThemeForest.Text = _loc["ThemeForest"];
        TextThemeSunset.Text = _loc["ThemeSunset"];
        ToolTipService.SetToolTip(TextThemeLight, _loc["ThemeLight"]);
        ToolTipService.SetToolTip(TextThemeDark, _loc["ThemeDark"]);
        ToolTipService.SetToolTip(TextThemeOcean, _loc["ThemeOcean"]);
        ToolTipService.SetToolTip(TextThemeForest, _loc["ThemeForest"]);
        ToolTipService.SetToolTip(TextThemeSunset, _loc["ThemeSunset"]);

        // Behavior
        TextBehaviorTitle.Text = _loc["Behavior"];
        TextBehaviorDesc.Text = _loc["BehaviorDesc"];
        TextAutoStart.Text = _loc["AutoStart"];
        TextAutoRefresh.Text = _loc["AutoRefresh"];
        TextShowNotifications.Text = _loc["ShowNotifications"];
        TextCloseToTray.Text = _loc["CloseToTray"];

        // Data Management
        TextDataTitle.Text = _loc["DataManagement"];
        TextDataDesc.Text = _loc["DataManagementDesc"];
        TextClearData.Text = _loc["ClearUsageData"];

        // About
        TextAboutTitle.Text = _loc["HeaderAbout"];
        TextAboutDesc.Text = _loc["HeaderAboutDesc"];
        TextAboutDescText.Text = _loc["AppDescription"];
        TextVersion.Text = $"{_loc["Version"]}: {_loc["AppVersion"]}";
    }

    private void LoadSettings()
    {
        var settings = AppSettings.Load();
        AutoStartToggle.IsOn = settings.AutoStart;
        AutoRefreshToggle.IsOn = settings.AutoRefresh;
        NotificationsToggle.IsOn = settings.ShowNotifications;
        CloseToTrayToggle.IsOn = settings.CloseToTray;
    }

    private void SaveSettings(Action<AppSettings> apply)
    {
        var settings = AppSettings.Load();
        apply(settings);
        settings.Save();
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
        var language = LanguageRadio.SelectedIndex == 0 ? AppLanguage.English : AppLanguage.Chinese;
        _loc.CurrentLanguage = language;
        SaveSettings(s => s.Language = language == AppLanguage.English ? "English" : "Chinese");
    }

    private void ThemeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeGrid.SelectedIndex < 0) return;
        var theme = (AppTheme)ThemeGrid.SelectedIndex;
        App.ChangeTheme(theme);
        SaveSettings(s => s.Theme = theme.ToString());
    }

    private void AutoStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;

        // Apply to the registry first; keep the toggle honest if it fails
        var applied = AutoStartHelper.SetEnabled(AutoStartToggle.IsOn);
        AutoStartToggle.IsOn = AutoStartHelper.IsEnabled();
        SaveSettings(s => s.AutoStart = AutoStartToggle.IsOn);
    }

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSettings(s => s.AutoRefresh = AutoRefreshToggle.IsOn);
    }

    private void Notifications_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        NotificationService.Instance.EnableNotifications = NotificationsToggle.IsOn;
        SaveSettings(s => s.ShowNotifications = NotificationsToggle.IsOn);
    }

    private void CloseToTray_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        SaveSettings(s => s.CloseToTray = CloseToTrayToggle.IsOn);
    }

    private async void BtnClearData_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = _loc["ClearUsageData"],
            Content = _loc["ClearDataConfirm"],
            PrimaryButtonText = _loc["Clear"],
            CloseButtonText = _loc["Cancel"],
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ThemeService.Instance.FluentTheme,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        // Clear the in-memory state first, otherwise the periodic save timer
        // would immediately write the deleted files back to disk.
        UsageTrackingService.Instance.ClearAllData();

        _viewModel?.Refresh();

        NotificationService.Instance.ShowToast(_loc["DataCleared"], "");
    }
}
