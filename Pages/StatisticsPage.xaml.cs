using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RubyDevice.Core;
using RubyDevice.Services;
using RubyDevice.ViewModels;

namespace RubyDevice.Pages;

public sealed partial class StatisticsPage : Page
{
    private MainViewModel? _viewModel;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public MainViewModel? ViewModel => _viewModel;

    public StatisticsPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateTexts();
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        TextTotal.Text = _loc["TotalDevices"];
        TextActive.Text = _loc["ActiveDevices"];
        TextDisabled.Text = _loc["DisabledDevices"];
        TextExternal.Text = _loc["ExternalDevices"];
        TextDistribution.Text = _loc["DeviceDistribution"];
        TextKeyboard.Text = _loc["Keyboard"];
        TextMouse.Text = _loc["Mouse"];
        TextTouchpad.Text = _loc["Touchpad"];
        TextOther.Text = _loc["Unknown"];
        TextUsage.Text = _loc["HeaderDevices"];
        TextRefresh.Text = _loc["Refresh"];
        TextTotalUsage.Text = _loc["HeaderStatistics"];
        TextUsageHint.Text = _loc["AppDescription"];
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel = e.Parameter as MainViewModel;
        Bindings.Update();
        UpdateStats();
        if (_viewModel != null)
            _viewModel.PropertyChanged += (_, _) => UpdateStats();
    }

    private void UpdateStats()
    {
        if (_viewModel == null) return;
        var devices = _viewModel.AllDevices;

        // Basic counts
        CountTotal.Text = devices.Count.ToString();
        CountActive.Text = devices.Count(d => d.IsEnabled).ToString();
        CountDisabled.Text = devices.Count(d => !d.IsEnabled).ToString();
        CountExternal.Text = devices.Count(d => d.IsExternal).ToString();

        // Type distribution
        var total = devices.Count > 0 ? devices.Count : 1;
        var keyboards = devices.Count(d => d.Type == DeviceType.Keyboard);
        var mice = devices.Count(d => d.Type == DeviceType.Mouse);
        var touchpads = devices.Count(d => d.Type == DeviceType.Touchpad);
        var others = devices.Count(d => d.Type == DeviceType.Unknown);

        // Update bar widths (max width ~300)
        double maxWidth = 200;
        BarKeyboard.Width = (keyboards * maxWidth) / total;
        BarMouse.Width = (mice * maxWidth) / total;
        BarTouchpad.Width = (touchpads * maxWidth) / total;
        BarOther.Width = (others * maxWidth) / total;

        // Update counts
        CountKeyboard.Text = keyboards.ToString();
        CountMouse.Text = mice.ToString();
        CountTouchpad.Text = touchpads.ToString();
        CountOther.Text = others.ToString();

        // Usage list - sort by usage time descending
        var sortedDevices = devices.OrderByDescending(d => d.TotalUsageSeconds).ToList();
        UsageList.ItemsSource = sortedDevices;

        // Show/hide no devices text
        TextNoDevices.Visibility = devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Total usage time
        var totalSeconds = devices.Sum(d => d.TotalUsageSeconds);
        var hours = totalSeconds / 3600;
        var mins = (totalSeconds % 3600) / 60;
        TotalUsageTime.Text = hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.Refresh();
        UpdateStats();
    }
}
