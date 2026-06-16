using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Pages;

public sealed partial class AboutPage : Page
{
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public AboutPage()
    {
        InitializeComponent();
        _loc.PropertyChanged += (_, _) => UpdateLocalizedStrings();
        UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        AppDescription.Text = "A modern input device management tool for Windows. Control your keyboard, mouse, and touchpad devices with ease.";
        FeaturesTitle.Text = _loc["Features"];
        Feature1.Text = _loc["Feature1"];
        Feature2.Text = _loc["Feature2"];
        Feature3.Text = _loc["Feature3"];
        Feature4.Text = _loc["Feature4"];
    }
}