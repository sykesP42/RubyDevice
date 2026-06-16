using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using RubyDevice.Services;

namespace RubyDevice.Controls;

public sealed partial class EnableConfirmDialog : ContentDialog
{
    public string DialogTitle { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    /// <summary>
    /// 创建启用设备确认弹窗
    /// </summary>
    /// <param name="deviceName">设备名称</param>
    /// <param name="onConfirm">确认后执行的操作</param>
    public EnableConfirmDialog(string deviceName, Action onConfirm)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        DialogTitle = loc["EnableConfirmTitle"];
        Message = loc["EnableConfirmMessage"];
        ConfirmText = loc["ConfirmSave"];
        CancelText = loc["Cancel"];
    }

    public async Task<bool> ShowAndConfirmAsync()
    {
        var result = await ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}