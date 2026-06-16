using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

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
        Loaded += (_, _) => _initialized = true;
    }

    private void UpdateTexts()
    {
        TextLanguageTitle.Text = _loc["Language"];
        TextThemeTitle.Text = _loc["Theme"];
        TextThemeLight.Text = _loc["ThemeLight"];
        TextThemeDark.Text = _loc["ThemeDark"];
        TextThemeOcean.Text = _loc["ThemeOcean"];
        TextThemeForest.Text = _loc["ThemeForest"];
        TextThemeSunset.Text = _loc["ThemeSunset"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
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
                AppTheme.Dark => new System.Uri("ms-appx:///Themes/DarkTheme.xaml"),
                AppTheme.Ocean => new System.Uri("ms-appx:///Themes/OceanTheme.xaml"),
                AppTheme.Forest => new System.Uri("ms-appx:///Themes/ForestTheme.xaml"),
                AppTheme.Sunset => new System.Uri("ms-appx:///Themes/SunsetTheme.xaml"),
                _ => new System.Uri("ms-appx:///Themes/LightTheme.xaml")
            }
        };
        merged.Add(themeDict);
    }
}