using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RubyDevice.Services;

public enum AppTheme
{
    Light,
    Dark,
    Ocean,
    Forest,
    Sunset
}

public class ThemeService : INotifyPropertyChanged
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private AppTheme _currentTheme = AppTheme.Light;

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