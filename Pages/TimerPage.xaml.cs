using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class TimerPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    private bool _initialized = false;

    public TimerPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateLocalizedStrings();
        UpdateLocalizedStrings();

        // Setup NumberBox change handler after initialization
        TimeoutBox.ValueChanged += TimeoutBox_ValueChanged;
        _initialized = true;
    }

    private void UpdateLocalizedStrings()
    {
        TimerTitle.Text = _loc["AutoRestoreTimer"];
        TimerDesc.Text = _loc["AutoRestoreDesc"];
        EnableTimerToggle.Header = _loc["EnableAutoRestore"];
        TimeoutBox.Header = _loc["TimeoutMinutes"];
        PresetsTitle.Text = _loc["QuickPresets"];
        DisabledTitle.Text = _loc["DisabledDevicesList"];
        DisabledDesc.Text = _loc["DisabledListDesc"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        UpdateDisabledList();
        if (_viewModel != null)
            _viewModel.PropertyChanged += (_, _) => UpdateDisabledList();
    }

    private void UpdateDisabledList()
    {
        if (_viewModel == null) return;
        DisabledList.ItemsSource = _viewModel.AllDevices.Where(d => !d.IsEnabled);
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string minutes)
        {
            var mins = int.Parse(minutes);
            TimeoutBox.Value = mins;
            UpdateTimerDisplay(mins);
        }
    }

    private void UpdateTimerDisplay(int minutes)
    {
        TimerDisplay.Text = $"{minutes}:00";
    }

    private void TimeoutBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_initialized) return;
        if (args.NewValue > 0)
        {
            UpdateTimerDisplay((int)args.NewValue);
        }
    }
}