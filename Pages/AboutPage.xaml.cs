using System;
using Microsoft.UI.Xaml;
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
        VersionText.Text = $"{_loc["Version"]} {_loc["AppVersion"]}";
        AppDesc.Text = _loc["AppDescription"];
        FeaturesTitle.Text = _loc["Features"];
        TechInfoTitle.Text = _loc["TechInfo"];
        TechFramework.Text = _loc["Framework"];
        TechPlatform.Text = _loc["Platform"];
        TechSdk.Text = _loc["SDK"];
        TechLicense.Text = _loc["License"];
        LinksTitle.Text = _loc["Links"];
        GithubText.Text = _loc["GithubRepo"];
        CopyrightText.Text = string.Format(_loc["Copyright"], DateTime.Now.Year);

        BuildFeaturesList();
    }

    private void BuildFeaturesList()
    {
        FeaturesList.Children.Clear();

        var features = new[]
        {
            "Feature1", "Feature2", "Feature3", "Feature4",
            "Feature5", "Feature6", "Feature7", "Feature8"
        };

        foreach (var key in features)
        {
            var text = _loc[key];
            if (text == key) continue; // skip if no localization

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(20) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 8
            };

            var icon = new FontIcon
            {
                Glyph = "",
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SuccessBrush"]
            };
            Grid.SetColumn(icon, 0);

            var block = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"]
            };
            Grid.SetColumn(block, 1);

            grid.Children.Add(icon);
            grid.Children.Add(block);
            FeaturesList.Children.Add(grid);
        }
    }
}
