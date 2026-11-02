using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

/// <summary>
/// Simple confirmation dialog shown before enabling a device.
/// </summary>
public sealed partial class EnableConfirmDialog : ContentDialog
{
    public string DialogTitle { get; }
    public string Message { get; }
    public string DeviceName { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    /// <summary>
    /// Creates the enable confirmation dialog
    /// </summary>
    /// <param name="deviceName">Device name</param>
    public EnableConfirmDialog(string deviceName)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["EnableConfirmTitle"];
        Message = loc["EnableConfirmMessage"];
        DeviceName = deviceName;
        ConfirmText = loc["Confirm"];
        CancelText = loc["Cancel"];
    }

    /// <summary>
    /// Shows the dialog.
    /// </summary>
    /// <returns>True when the user confirmed</returns>
    public async Task<bool> ShowAndConfirmAsync()
    {
        var result = await ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
