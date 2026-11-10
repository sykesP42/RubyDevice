using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using RubyDevice.Models;
using RubyDevice.Services;

namespace RubyDevice;

/// <summary>
/// Application entry point. Creates the main window and handles unhandled exceptions.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// The running application instance, or null during shutdown
    /// </summary>
    public static App? CurrentApp => (App?)Application.Current;

    /// <summary>
    /// The main application window, or null before it has been created
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the application and installs the unhandled exception handler
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Loads persisted settings, applies the saved language and theme, creates
    /// and activates the main window. Only one instance may run: two copies
    /// would both install low-level hooks and fight over input blocking.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\RubyDevice.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            var loc = LocalizationService.Instance;
            MessageBoxW(IntPtr.Zero, loc["AlreadyRunningMessage"], loc["AlreadyRunningTitle"], 0x40 /* MB_ICONINFORMATION */);
            Exit();
            return;
        }

        // Restore language and theme before the window is built so the first
        // frame already uses the user's preferences.
        var settings = AppSettings.Load();
        LocalizationService.Instance.CurrentLanguage =
            settings.Language == "Chinese" ? AppLanguage.Chinese : AppLanguage.English;
        if (Enum.TryParse<AppTheme>(settings.Theme, out var theme))
        {
            ThemeService.Instance.CurrentTheme = theme;
        }

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// Apply a theme by swapping the active resource dictionary and forcing
    /// already-created UI to re-evaluate its {ThemeResource} bindings.
    /// </summary>
    public static void ChangeTheme(AppTheme theme)
    {
        ThemeService.Instance.CurrentTheme = theme;

        if (Current?.Resources == null) return;

        var merged = Current.Resources.MergedDictionaries;
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

        // StaticResource references baked into existing elements keep the old
        // brushes, so re-evaluate by flipping the requested theme. Toggling
        // RequestedTheme makes every {ThemeResource} reference in the subtree
        // re-resolve, picking up the newly merged dictionary.
        //
        // The final theme must be explicit (not Default): the custom palettes are
        // hand-rolled, so the Fluent theme has to be forced to match - a system
        // dark mode would otherwise render default control text white on the
        // light palettes.
        if (MainWindow?.Content is FrameworkElement fe)
        {
            var target = theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
            var opposite = target == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
            fe.RequestedTheme = opposite;
            fe.RequestedTheme = target;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RubyDevice");
            // The directory may not exist yet on a first-run crash
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n");
        }
        catch
        {
            // Never let the crash logger itself crash the app
        }
        e.Handled = true;
    }
}
