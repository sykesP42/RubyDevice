using System;
using System.IO;
using Microsoft.UI.Xaml;
using RubyDevice.Services;

namespace RubyDevice;

public partial class App : Application
{
    public static App? CurrentApp => (App?)Application.Current;
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (sender, e) =>
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RubyDevice", "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    public static void ChangeTheme(AppTheme theme)
    {
        var themeService = ThemeService.Instance;
        themeService.CurrentTheme = theme;

        if (Current?.Resources == null) return;

        var merged = Current.Resources.MergedDictionaries;
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

        // Force window to refresh
        if (MainWindow?.Content is FrameworkElement fe)
        {
            fe.RequestedTheme = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }
}