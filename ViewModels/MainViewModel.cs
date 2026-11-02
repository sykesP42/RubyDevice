using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using RubyDevice.Core;
using RubyDevice.Models;
using RubyDevice.Services;

namespace RubyDevice.ViewModels;

/// <summary>
/// ViewModel representing a single device in the UI
/// </summary>
public class DeviceViewModel : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private string _userNote = "";
    private bool _isTracking;
    private WeakReference<MainViewModel>? _parentViewModel;

    /// <summary>Stable physical device identifier</summary>
    public string DeviceId { get; set; } = "";
    /// <summary>Display name of the device</summary>
    public string Name { get; set; } = "";
    /// <summary>Device manufacturer (from known vendor list or VID)</summary>
    public string Manufacturer { get; set; } = "";
    /// <summary>Device type (Keyboard/Mouse/Touchpad)</summary>
    public DeviceType Type { get; set; }
    /// <summary>True for external (non-built-in) devices</summary>
    public bool IsExternal { get; set; }
    /// <summary>USB vendor ID as hex string</summary>
    public string VendorId { get; set; } = "";
    /// <summary>USB product ID as hex string</summary>
    public string ProductId { get; set; } = "";
    /// <summary>Total usage time in seconds</summary>
    public long TotalUsageSeconds { get; set; }

    // Reference to parent view model for checking active state
    /// <summary>
    /// Stores a weak reference to the parent view model so the device does not
    /// keep it alive
    /// </summary>
    internal void SetParentViewModel(MainViewModel parent)
    {
        _parentViewModel = new WeakReference<MainViewModel>(parent);
    }

    /// <summary>
    /// Re-synchronizes all display properties from the device descriptor and
    /// raises change notifications so existing bindings stay live
    /// </summary>
    internal void ApplyFrom(DeviceInfo info, bool isTracking, double totalActiveSeconds)
    {
        DeviceId = info.DeviceId;
        Name = info.Name;
        Manufacturer = info.Manufacturer;
        Type = info.Type;
        IsExternal = info.IsExternal;
        VendorId = info.VendorId;
        ProductId = info.ProductId;
        TotalUsageSeconds = (long)Math.Round(totalActiveSeconds);
        _isEnabled = info.IsEnabled;
        _userNote = info.UserNote;
        _isTracking = isTracking;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Manufacturer));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(IconGlyph));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(UsageTimeText));
        OnPropertyChanged(nameof(UserNote));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(IsTracking));
        OnPropertyChanged(nameof(TrackingIconVisibility));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActiveBrush));
    }

    /// <summary>
    /// Refreshes the bindings that depend on whether this device is active
    /// </summary>
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
    /// Highlight brush for the row of the device currently receiving input.
    /// Uses the theme's light primary brush so it adapts to the active theme.
    /// </summary>
    public Microsoft.UI.Xaml.Media.Brush ActiveBrush => IsActive
        ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PrimaryLightBrush"]
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private bool _isTrackingBacking;
    /// <summary>
    /// Indicates whether usage tracking is enabled for this device.
    /// The getter queries the tracking service so state stays fresh; the setter
    /// only updates the notification state (the service is updated by callers).
    /// </summary>
    public bool IsTracking
    {
        get
        {
            if (string.IsNullOrEmpty(DeviceId)) return _isTrackingBacking;
            return UsageTrackingService.Instance.IsTracking(DeviceId);
        }
        set
        {
            if (_isTrackingBacking != value)
            {
                _isTrackingBacking = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrackingIconVisibility));
            }
        }
    }

    /// <summary>
    /// User-entered note shown alongside the device in the device list
    /// </summary>
    public string UserNote
    {
        get => _userNote;
        set { _userNote = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNote)); }
    }

    /// <summary>
    /// Visibility of the note indicator, shown only when a note has been entered
    /// </summary>
    public Visibility HasNote => !string.IsNullOrWhiteSpace(_userNote) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Localized tooltip for the note indicator</summary>
    public string HasNoteText => LocalizationService.Instance["HasNote"];

    /// <summary>Localized tooltip for the tracking indicator</summary>
    public string TrackingText => LocalizationService.Instance["TrackingEnabled"];

    /// <summary>
    /// Visibility of the tracking indicator icon
    /// </summary>
    public Visibility TrackingIconVisibility => IsTracking ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Whether the device is currently enabled
    /// </summary>
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
            }
        }
    }

    /// <summary>
    /// Localized device type name used in the device list
    /// </summary>
    public string TypeName => Type switch
    {
        DeviceType.Keyboard => LocalizationService.Instance["Keyboard"],
        DeviceType.Mouse => LocalizationService.Instance["Mouse"],
        DeviceType.Touchpad => LocalizationService.Instance["Touchpad"],
        _ => LocalizationService.Instance["Unknown"]
    };

    /// <summary>
    /// Segoe MDL2 glyph representing the device type
    /// </summary>
    public string IconGlyph => Type switch
    {
        DeviceType.Keyboard => "\uE765",
        DeviceType.Mouse => "\uE962",
        DeviceType.Touchpad => "\uE9CE",
        _ => "\uE7BA"  // Question mark
    };

    /// <summary>
    /// Localized enabled/disabled label for the device
    /// </summary>
    public string StatusText => IsEnabled
        ? LocalizationService.Instance["Enabled"]
        : LocalizationService.Instance["Disabled"];

    /// <summary>
    /// Localized label describing whether the device is external or built in
    /// </summary>
    public string LocationText => IsExternal
        ? LocalizationService.Instance["External"]
        : LocalizationService.Instance["BuiltIn"];

    /// <summary>
    /// Total usage time formatted for display, as hours and minutes or minutes alone
    /// </summary>
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

/// <summary>
/// Groups the devices of one type together for display in the device list
/// </summary>
public class DeviceGroupViewModel
{
    /// <summary>Device type this group represents</summary>
    public DeviceType Type { get; }
    /// <summary>Localized group name (e.g. "Keyboards")</summary>
    public string Name { get; }
    /// <summary>MDL2 icon glyph for the group</summary>
    public string IconGlyph { get; }
    /// <summary>Localized batch-enable button label</summary>
    public string EnableAllText { get; }
    /// <summary>Localized batch-disable button label</summary>
    public string DisableAllText { get; }
    /// <summary>Number of devices in this group</summary>
    public string CountText => Devices.Count > 0 ? Devices.Count.ToString() : "0";
    /// <summary>Visible when at least one device is enabled</summary>
    public Visibility HasEnabled => Devices.Any(d => d.IsEnabled) ? Visibility.Visible : Visibility.Collapsed;
    /// <summary>Visible when at least one device is disabled</summary>
    public Visibility HasDisabled => Devices.Any(d => !d.IsEnabled) ? Visibility.Visible : Visibility.Collapsed;
    /// <summary>Devices belonging to this group</summary>
    public ObservableCollection<DeviceViewModel> Devices { get; } = new();

    /// <summary>
    /// Creates a group for the given device type and resolves its localized name
    /// </summary>
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
            DeviceType.Keyboard => "\uE765",
            DeviceType.Mouse => "\uE962",
            DeviceType.Touchpad => "\uE9CE",
            _ => "\uE7BA"
        };
        EnableAllText = loc["EnableAll"];
        DisableAllText = loc["DisableAll"];
    }
}

/// <summary>
/// Main application view model. Manages the device collection, filtering/sorting,
/// activity highlighting, device enable/disable operations, and the app-wide
/// auto-restore countdown.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly DeviceManager _deviceManager = new();
    private readonly DispatcherQueue? _dispatcherQueue;
    private DeviceType? _filterType;

    /// <summary>First-stage disable confirmation countdown, in seconds</summary>
    public const int DisableConfirmSeconds = 5;
    /// <summary>Auto-restore countdown shown after a device is disabled, in seconds</summary>
    public const int AutoRestoreSeconds = 10;

    #region Collections

    /// <summary>Flat list of every enumerated device</summary>
    public ObservableCollection<DeviceViewModel> AllDevices { get; } = new();
    /// <summary>Devices grouped by type, rebuilt whenever the device list changes</summary>
    public ObservableCollection<DeviceGroupViewModel> DeviceGroups { get; } = new();

    #endregion

    #region Activity Highlight

    private bool _isActivityHighlightEnabled = true;
    /// <summary>
    /// Whether the device list highlights the device currently receiving input
    /// </summary>
    public bool IsActivityHighlightEnabled
    {
        get => _isActivityHighlightEnabled;
        set
        {
            if (_isActivityHighlightEnabled != value)
            {
                _isActivityHighlightEnabled = value;
                OnPropertyChanged();
                if (!value) ActiveDeviceId = null;
            }
        }
    }

    private string? _activeDeviceId;
    /// <summary>
    /// Identifier of the device most recently seen producing input, or null when
    /// highlighting is disabled
    /// </summary>
    public string? ActiveDeviceId
    {
        get => _activeDeviceId;
        set
        {
            if (!_isActivityHighlightEnabled && value != null) return;
            if (_activeDeviceId != value)
            {
                _activeDeviceId = value;
                OnPropertyChanged();
                foreach (var d in AllDevices)
                    d.OnIsActiveChanged();
            }
        }
    }

    #endregion

    #region Filter & Sort

    /// <summary>
    /// Restricts the device list to a single device type, or null for all types
    /// </summary>
    public DeviceType? FilterType
    {
        get => _filterType;
        set { _filterType = value; OnPropertyChanged(); UpdateGroups(); }
    }

    private string _searchText = "";
    /// <summary>
    /// Free text matched against device name, manufacturer and location
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); UpdateGroups(); }
    }

    private string _statusFilter = "All";
    /// <summary>
    /// Filters the list by device state; one of "All", "Enabled" or "Disabled"
    /// </summary>
    public string StatusFilter
    {
        get => _statusFilter;
        set { _statusFilter = value; OnPropertyChanged(); UpdateGroups(); }
    }

    private int _sortMode;
    /// <summary>
    /// Active sort order: 0 by name, 1 by type, 2 by status
    /// </summary>
    public int SortMode
    {
        get => _sortMode;
        set { _sortMode = value; OnPropertyChanged(); UpdateGroups(); }
    }

    #endregion

    /// <summary>
    /// Creates the view model and subscribes to device activity and hotplug events
    /// </summary>
    public MainViewModel()
    {
        // Captured on the UI thread where the view model is constructed; the
        // device watcher raises its events from a thread-pool thread.
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Subscribe to device activity events
        _deviceManager.DeviceActivity += OnDeviceActivity;
        _deviceManager.DevicesEmergencyUnlocked += OnDevicesEmergencyUnlocked;

        // Subscribe to device change events (hotplug)
        DeviceWatcherService.Instance.DevicesChanged += OnDevicesChanged;
    }

    #region Event Handlers

    private void OnDeviceActivity(object? sender, DeviceManager.DeviceActivityEventArgs e)
    {
        // Update active device ID (will trigger UI update via binding)
        ActiveDeviceId = e.DeviceId;
    }

    private void OnDevicesEmergencyUnlocked(object? sender, EventArgs e)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            StopAutoRestore();
            Refresh();
            var loc = LocalizationService.Instance;
            NotificationService.Instance.ShowToast(loc["EmergencyUnlock"], loc["EmergencyUnlockMessage"]);
        });
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        var settings = AppSettings.Load();
        if (!settings.AutoRefresh) return;

        _dispatcherQueue?.TryEnqueue(() =>
        {
            var before = AllDevices.Select(d => (d.DeviceId, d.IsEnabled)).ToList();
            Refresh();

            // Only notify when the device list actually changed; the WMI event
            // fires for every device class, not just input devices.
            var after = AllDevices.Select(d => (d.DeviceId, d.IsEnabled)).ToList();
            if (!before.SequenceEqual(after))
            {
                var loc = LocalizationService.Instance;
                NotificationService.Instance.ShowToast(loc["DevicesChanged"], loc["DeviceListRefreshed"]);
            }
        });
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Performs the initial device enumeration and starts watching for device changes
    /// </summary>
    public void Initialize()
    {
        Refresh();
        DeviceWatcherService.Instance.Start();
    }

    #endregion

    #region Device Management

    /// <summary>
    /// Exposes the underlying device manager for pages that need to inspect or
    /// toggle devices directly
    /// </summary>
    public DeviceManager GetDeviceManager() => _deviceManager;

    /// <summary>
    /// Re-enumerates devices and updates the device view models in place, keeping
    /// object identity stable for pages that hold references to them
    /// </summary>
    public void Refresh()
    {
        _deviceManager.RefreshDevices();

        var existing = AllDevices.ToDictionary(d => d.DeviceId, d => d);
        var seen = new HashSet<string>();

        foreach (var info in _deviceManager.GetDevicesSnapshot())
        {
            seen.Add(info.DeviceId);
            var isTracking = UsageTrackingService.Instance.IsTracking(info.DeviceId);
            var totalActive = UsageTrackingService.Instance.GetTotalActiveSeconds(info.DeviceId);

            if (existing.TryGetValue(info.DeviceId, out var vm))
            {
                vm.ApplyFrom(info, isTracking, totalActive);
            }
            else
            {
                var newVm = new DeviceViewModel();
                newVm.SetParentViewModel(this);
                newVm.ApplyFrom(info, isTracking, totalActive);
                AllDevices.Add(newVm);
            }
        }

        foreach (var vm in AllDevices.Where(d => !seen.Contains(d.DeviceId)).ToList())
            AllDevices.Remove(vm);

        UpdateGroups();
    }

    /// <summary>
    /// Rebuilds DeviceGroups applying the current type filter, status filter,
    /// search text, and sort mode
    /// </summary>
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

    /// <summary>
    /// Persist device notes to disk via the device manager's cache
    /// </summary>
    public void SaveDeviceData()
    {
        _deviceManager.SaveCache();
    }

    /// <summary>
    /// Set a user note for a device and persist it
    /// </summary>
    public void SetDeviceNote(string deviceId, string note)
    {
        var device = AllDevices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device != null) device.UserNote = note;
        SaveDeviceData();
    }

    /// <summary>
    /// Actually enable/disable a device through the device manager's hooks
    /// </summary>
    public bool ToggleDevice(string deviceId, bool enable)
    {
        return _deviceManager.ToggleDevice(deviceId, enable);
    }

    /// <summary>
    /// Re-enables every currently disabled device and returns how many were restored
    /// </summary>
    public int RestoreAllDisabledDevices()
    {
        var restored = 0;
        foreach (var device in AllDevices.Where(d => !d.IsEnabled).ToList())
        {
            if (ToggleDevice(device.DeviceId, true))
            {
                device.IsEnabled = true;
                restored++;
            }
        }
        return restored;
    }

    #endregion

    #region Enable / Disable Confirm Flows

    /// <summary>
    /// Disable a device with the two-stage confirmation: a 5-second confirm
    /// countdown, then a 10-second auto-restore countdown the user can cancel.
    /// </summary>
    public async Task DisableDeviceWithConfirmAsync(DeviceViewModel device, UIElement root)
    {
        // Stage 1: confirmation with countdown
        var confirmDialog = new Controls.DisableConfirmDialog(device.Name, DisableConfirmSeconds);
        if (root?.XamlRoot != null)
            confirmDialog.XamlRoot = root.XamlRoot;

        if (!await confirmDialog.ShowWithCountdownAsync())
        {
            return; // User cancelled
        }

        if (!ToggleDevice(device.DeviceId, false))
        {
            var loc = LocalizationService.Instance;
            NotificationService.Instance.ShowToast(loc["ActionFailed"], device.Name);
            return;
        }

        device.IsEnabled = false;
        NotificationService.Instance.ShowDeviceDisabledNotification(device.Name, AutoRestoreSeconds);

        // Stage 2: auto-restore countdown
        var restoreDialog = new Controls.AutoRestoreDialog(device.Name, AutoRestoreSeconds);
        if (root?.XamlRoot != null)
            restoreDialog.XamlRoot = root.XamlRoot;

        if (await restoreDialog.ShowAndCountdownAsync())
        {
            EnableDevice(device);
        }
    }

    /// <summary>
    /// Enable a device after a simple confirmation
    /// </summary>
    public async Task EnableDeviceWithConfirmAsync(DeviceViewModel device, UIElement root)
    {
        var dialog = new Controls.EnableConfirmDialog(device.Name);
        if (root?.XamlRoot != null)
            dialog.XamlRoot = root.XamlRoot;

        if (await dialog.ShowAndConfirmAsync())
        {
            EnableDevice(device);
        }
    }

    private void EnableDevice(DeviceViewModel device)
    {
        if (ToggleDevice(device.DeviceId, true))
        {
            device.IsEnabled = true;
            NotificationService.Instance.ShowDeviceEnabledNotification(device.Name);
        }
    }

    #endregion

    #region Auto-Restore Countdown

    private DispatcherTimer? _autoRestoreTimer;
    private DateTime _autoRestoreDeadline;
    private int _autoRestoreTotalSeconds;
    private bool _isAutoRestoreRunning;

    /// <summary>Whether the auto-restore countdown is currently running.
    /// The countdown lives here so it keeps running when the Timer page is closed.</summary>
    public bool IsAutoRestoreRunning
    {
        get => _isAutoRestoreRunning;
        private set
        {
            if (_isAutoRestoreRunning != value)
            {
                _isAutoRestoreRunning = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Total configured countdown duration in seconds</summary>
    public int AutoRestoreTotalSeconds => _autoRestoreTotalSeconds;

    /// <summary>Seconds left on the auto-restore countdown</summary>
    public int AutoRestoreRemainingSeconds => IsAutoRestoreRunning
        ? Math.Max(0, (int)Math.Ceiling((_autoRestoreDeadline - DateTime.Now).TotalSeconds))
        : 0;

    /// <summary>Raised roughly once per second while the countdown runs</summary>
    public event EventHandler? AutoRestoreTick;

    /// <summary>
    /// Starts the auto-restore countdown. Returns false when it is already
    /// running or no devices are disabled.
    /// </summary>
    public bool StartAutoRestore(int minutes)
    {
        if (IsAutoRestoreRunning) return false;
        if (!AllDevices.Any(d => !d.IsEnabled)) return false;

        _autoRestoreTotalSeconds = Math.Max(1, minutes) * 60;
        // Deadline-based so tick lag never accumulates drift
        _autoRestoreDeadline = DateTime.Now.AddSeconds(_autoRestoreTotalSeconds);
        IsAutoRestoreRunning = true;
        OnPropertyChanged(nameof(AutoRestoreRemainingSeconds));

        _autoRestoreTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoRestoreTimer.Tick += OnAutoRestoreTick;
        _autoRestoreTimer.Start();
        return true;
    }

    /// <summary>
    /// Stops the auto-restore countdown without restoring devices
    /// </summary>
    public void StopAutoRestore()
    {
        if (_autoRestoreTimer != null)
        {
            _autoRestoreTimer.Stop();
            _autoRestoreTimer.Tick -= OnAutoRestoreTick;
            _autoRestoreTimer = null;
        }
        IsAutoRestoreRunning = false;
    }

    private void OnAutoRestoreTick(object? sender, object e)
    {
        OnPropertyChanged(nameof(AutoRestoreRemainingSeconds));
        AutoRestoreTick?.Invoke(this, EventArgs.Empty);

        if (AutoRestoreRemainingSeconds > 0) return;

        var restored = RestoreAllDisabledDevices();
        StopAutoRestore();

        if (restored > 0)
        {
            var loc = LocalizationService.Instance;
            NotificationService.Instance.ShowToast(
                loc["DeviceRestored"],
                string.Format(loc["DevicesRestoredCount"], restored));
        }
    }

    #endregion

    #region INotifyPropertyChanged

    /// <summary>
    /// Raised when a bound property value changes so the UI can refresh
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the calling member
    /// </summary>
    public void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    #endregion
}
