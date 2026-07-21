using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RubyDevice.Services;

/// <summary>
/// Application theme variants available in RubyDevice
/// </summary>
public enum AppTheme
{
    Light,
    Dark,
    Ocean,
    Forest,
    Sunset
}

/// <summary>
/// Manages the active application theme and notifies UI when it changes.
/// The current theme is applied via ThemeService in App.xaml.cs.
/// </summary>
public class ThemeService : INotifyPropertyChanged
{
    private static ThemeService? _instance;

    /// <summary>
    /// Gets the singleton ThemeService instance
    /// </summary>
    public static ThemeService Instance => _instance ??= new ThemeService();

    private AppTheme _currentTheme = AppTheme.Light;

    /// <summary>
    /// Gets or sets the currently active theme
    /// </summary>
    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Resource file path for the active theme
    /// </summary>
    public string ThemeResourcePath => CurrentTheme switch
    {
        AppTheme.Dark => "Themes/DarkTheme.xaml",
        AppTheme.Ocean => "Themes/OceanTheme.xaml",
        AppTheme.Forest => "Themes/ForestTheme.xaml",
        AppTheme.Sunset => "Themes/SunsetTheme.xaml",
        _ => "Themes/LightTheme.xaml"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}