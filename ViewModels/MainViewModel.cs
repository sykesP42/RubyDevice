using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RubyDevice.Core;
using RubyDevice.Services;

namespace RubyDevice.ViewModels;

public class DeviceViewModel : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private string _userNote = "";
    private WeakReference<MainViewModel>? _parentViewModel;

    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public DeviceType Type { get; set; }
    public bool IsExternal { get; set; }
    public string VendorId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public long TotalUsageSeconds { get; set; }

    // Reference to parent view model for checking active state
    internal void SetParentViewModel(MainViewModel parent)
    {
        _parentViewModel = new WeakReference<MainViewModel>(parent);
    }

    internal void OnIsActiveChanged()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActiveBrush));
    }

    /// <summary>
    /// Indicates if this device is currently active (receiving input)
    /// </summary>
    public bool IsActive
    {
        get
        {
            if (_parentViewModel != null && _parentViewModel.TryGetTarget(out var parent))
            {
                return parent.ActiveDeviceId == DeviceId;
            }
            return false;
        }
    }

    /// <summary>
    /// Highlight brush for active device
    /// </summary>
    public SolidColorBrush ActiveBrush => IsActive
        ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 59, 130, 246)) // Blue highlight
        : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private bool _isTracking;
    public bool IsTracking
    {
        get
        {
            // Query the service directly to ensure fresh state
            return Services.UsageTrackingService.Instance.IsTracking(DeviceId);
        }
        set
        {
            if (_isTracking != value)
            {
                _isTracking = value;
                OnPropertyChanged();
            }
        }
    }

    public string UserNote
    {
        get => _userNote;
        set { _userNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
    }

    public Visibility HasNote => !string.IsNullOrWhiteSpace(_userNote) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TrackingIconVisibility => IsTracking ? Visibility.Visible : Visibility.Collapsed;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColorBrush));
            }
        }
    }

    public string TypeName => Type switch
    {
        DeviceType.Keyboard => LocalizationService.Instance["Keyboard"],
        DeviceType.Mouse => LocalizationService.Instance["Mouse"],
        DeviceType.Touchpad => LocalizationService.Instance["Touchpad"],
        _ => LocalizationService.Instance["Unknown"]
    };

    public string IconGlyph => Type switch
    {
        DeviceType.Keyboard => "",
        DeviceType.Mouse => "",
        DeviceType.Touchpad => "",
        _ => ""  // Question mark icon
    };

    public string StatusText => IsEnabled
        ? LocalizationService.Instance["Enabled"]
        : LocalizationService.Instance["Disabled"];

    public SolidColorBrush StatusColorBrush => new SolidColorBrush(
        IsEnabled ? Microsoft.UI.ColorHelper.FromArgb(255, 209, 250, 229) : Microsoft.UI.ColorHelper.FromArgb(255, 254, 226, 226));

    public string LocationText => IsExternal
        ? LocalizationService.Instance["External"]
        : LocalizationService.Instance["BuiltIn"];

    public string UsageTimeText
    {
        get
        {
            var hours = TotalUsageSeconds / 3600;
            var mins = (TotalUsageSeconds % 3600) / 60;
            return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class DeviceGroupViewModel
{
    public DeviceType Type { get; }
    public string Name { get; }
    public string IconGlyph { get; }
    public string CountText => Devices.Count > 0 ? Devices.Count.ToString() : "0";
    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    public DeviceGroupViewModel(DeviceType type)
    {
        Type = type;
        var loc = LocalizationService.Instance;
        Name = type switch
        {
            DeviceType.Keyboard => loc["Keyboards"],
            DeviceType.Mouse => loc["Mice"],
            DeviceType.Touchpad => loc["Touchpads"],
            _ => loc["OtherDevices"]
        };
        IconGlyph = type switch
        {
            DeviceType.Keyboard => "",
            DeviceType.Mouse => "",
            DeviceType.Touchpad => "",
            _ => ""
        };
    }
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DeviceManager _deviceManager = new();
    private DeviceType? _filterType;
    private readonly string _dataPath;

    public ObservableCollection<DeviceViewModel> AllDevices { get; } = new();
    public ObservableCollection<DeviceGroupViewModel> DeviceGroups { get; } = new();

    // Activity highlight toggle
    private bool _isActivityHighlightEnabled = true;
    public bool IsActivityHighlightEnabled
    {
        get => _isActivityHighlightEnabled;
        set
        {
            if (_isActivityHighlightEnabled != value)
            {
                _isActivityHighlightEnabled = value;
                OnPropertyChanged();
                if (!value)
                {
                    ActiveDeviceId = null;
                }
            }
        }
    }

    // Active device tracking for UI highlighting
    private string? _activeDeviceId;
    public string? ActiveDeviceId
    {
        get => _activeDeviceId;
        set
        {
            // Only update if highlight is enabled
            if (!_isActivityHighlightEnabled && value != null) return;

            if (_activeDeviceId != value)
            {
                _activeDeviceId = value;
                OnPropertyChanged();
                // Notify all devices to update their active state
                foreach (var d in AllDevices)
                    d.OnIsActiveChanged();
            }
        }
    }

    public DeviceType? FilterType
    {
        get => _filterType;
        set { _filterType = value; OnPropertyChanged(); UpdateGroups(); }
    }

    // Search and sort
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); UpdateGroups(); }
    }

    private string _statusFilter = "All"; // "All", "Enabled", "Disabled"
    public string StatusFilter
    {
        get => _statusFilter;
        set { _statusFilter = value; OnPropertyChanged(); UpdateGroups(); }
    }

    private int _sortMode; // 0=Name, 1=Type, 2=Status
    public int SortMode
    {
        get => _sortMode;
        set { _sortMode = value; OnPropertyChanged(); UpdateGroups(); }
    }

    public MainViewModel()
    {
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RubyDevice", "device_data.json");

        // Subscribe to device activity events
        _deviceManager.DeviceActivity += OnDeviceActivity;
    }

    private void OnDeviceActivity(object? sender, DeviceManager.DeviceActivityEventArgs e)
    {
        // Update active device ID (will trigger UI update via binding)
        ActiveDeviceId = e.DeviceId;
    }

    public void Initialize() => Refresh();

    public DeviceManager GetDeviceManager() => _deviceManager;

    public void Refresh()
    {
        _deviceManager.RefreshDevices();
        AllDevices.Clear();

        foreach (var d in _deviceManager.Devices)
        {
            var vm = new DeviceViewModel
            {
                DeviceId = d.DeviceId,
                Name = d.Name,
                Manufacturer = d.Manufacturer,
                Type = d.Type,
                IsEnabled = d.IsEnabled,
                IsExternal = d.IsExternal,
                VendorId = d.VendorId,
                ProductId = d.ProductId,
                UserNote = d.UserNote,
                TotalUsageSeconds = d.TotalUsageSeconds,
                IsTracking = Services.UsageTrackingService.Instance.IsTracking(d.DeviceId)
            };
            vm.SetParentViewModel(this);
            AllDevices.Add(vm);
        }
        UpdateGroups();
    }

    private void UpdateGroups()
    {
        DeviceGroups.Clear();
        IEnumerable<DeviceViewModel> filtered = AllDevices;

        // Apply type filter
        if (FilterType != null)
            filtered = filtered.Where(d => d.Type == FilterType);

        // Apply status filter
        if (StatusFilter == "Enabled")
            filtered = filtered.Where(d => d.IsEnabled);
        else if (StatusFilter == "Disabled")
            filtered = filtered.Where(d => !d.IsEnabled);

        // Apply search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(d =>
                d.Name.ToLowerInvariant().Contains(search) ||
                d.Manufacturer.ToLowerInvariant().Contains(search) ||
                d.DeviceId.ToLowerInvariant().Contains(search));
        }

        // Apply sorting
        filtered = SortMode switch
        {
            0 => filtered.OrderBy(d => d.Name),       // By name
            1 => filtered.OrderBy(d => d.Type),         // By type
            2 => filtered.OrderByDescending(d => d.IsEnabled).ThenBy(d => d.Name), // By status
            _ => filtered.OrderBy(d => d.Name)
        };

        // Group and build
        var grouped = filtered.GroupBy(d => d.Type).OrderBy(g => g.Key switch
        {
            DeviceType.Keyboard => 0,
            DeviceType.Mouse => 1,
            DeviceType.Touchpad => 2,
            _ => 3
        });

        foreach (var g in grouped)
        {
            var group = new DeviceGroupViewModel(g.Key);
            foreach (var d in g) group.Devices.Add(d);
            DeviceGroups.Add(group);
        }
    }

    public void SaveDeviceData()
    {
        try
        {
            var dir = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = AllDevices.ToDictionary(d => d.DeviceId, d => new { d.UserNote, d.TotalUsageSeconds });
            File.WriteAllText(_dataPath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    public void SetDeviceNote(string deviceId, string note)
    {
        var device = AllDevices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device != null) device.UserNote = note;
        SaveDeviceData();
    }

    /// <summary>
    /// 实际启用/禁用设备 - 调用 DeviceManager 的 SetupAPI
    /// </summary>
    public bool ToggleDevice(string deviceId, bool enable)
    {
        return _deviceManager.ToggleDevice(deviceId, enable);
    }

    /// <summary>
    /// 禁用设备（两阶段确认：5秒确认 + 10秒自动恢复）
    /// </summary>
    public async Task DisableDeviceWithConfirmAsync(DeviceViewModel device, UIElement root)
    {
        // 第一阶段：5秒倒计时确认
        bool confirmed = false;
        var confirmDialog = new Controls.DisableConfirmDialog(
            device.Name,
            5, // 5秒倒计时
            () => { confirmed = true; });

        if (root?.XamlRoot != null)
            confirmDialog.XamlRoot = root.XamlRoot;

        await confirmDialog.ShowWithCountdownAsync();

        if (!confirmed)
        {
            // 用户取消了
            return;
        }

        // 执行禁用
        if (!ToggleDevice(device.DeviceId, false))
        {
            return; // 禁用失败
        }

        device.IsEnabled = false;

        // 显示禁用通知
        NotificationService.Instance.ShowDeviceDisabledNotification(device.Name, 10);

        // 第二阶段：10秒自动恢复倒计时
        var restoreDialog = new Controls.AutoRestoreDialog(
            device.Name,
            10, // 10秒自动恢复
            () =>
            {
                // 恢复设备
                if (ToggleDevice(device.DeviceId, true))
                {
                    device.IsEnabled = true;
                    var loc = LocalizationService.Instance;
                    NotificationService.Instance.ShowToast(
                        loc["DeviceRestored"],
                        string.Format(loc["DeviceRestoredMessage"], device.Name));
                }
            },
            () =>
            {
                // 保持禁用
                var loc = LocalizationService.Instance;
                NotificationService.Instance.ShowToast(
                    loc["DeviceDisabled"],
                    device.Name);
            });

        if (root?.XamlRoot != null)
            restoreDialog.XamlRoot = root.XamlRoot;

        await restoreDialog.ShowAndCountdownAsync();
    }

    /// <summary>
    /// 启用设备（简单确认）
    /// </summary>
    public async Task EnableDeviceWithConfirmAsync(DeviceViewModel device, UIElement root)
    {
        var dialog = new Controls.EnableConfirmDialog(device.Name, () =>
        {
            if (ToggleDevice(device.DeviceId, true))
            {
                device.IsEnabled = true;
                NotificationService.Instance.ShowDeviceEnabledNotification(device.Name);
            }
        });

        if (root?.XamlRoot != null)
            dialog.XamlRoot = root.XamlRoot;

        await dialog.ShowAndConfirmAsync();
    }

    // 保留旧方法以兼容现有代码
    public async Task ToggleDeviceWithConfirmAsync(DeviceViewModel device, UIElement root)
    {
        await DisableDeviceWithConfirmAsync(device, root);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}