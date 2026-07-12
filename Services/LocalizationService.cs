using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RubyDevice.Services;

public enum AppLanguage
{
    English,
    Chinese
}

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private AppLanguage _currentLanguage = AppLanguage.English;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Strings));
            }
        }
    }

    // All localized strings
    public Dictionary<string, string> Strings => CurrentLanguage == AppLanguage.English
        ? EnglishStrings
        : ChineseStrings;

    private static readonly Dictionary<string, string> EnglishStrings = new()
    {
        // App
        ["AppName"] = "RubyDevice",
        ["AppDescription"] = "Input Device Controller",

        // Navigation
        ["NavDevices"] = "Devices",
        ["NavStatistics"] = "Statistics",
        ["NavTimer"] = "Timer",
        ["NavSettings"] = "Settings",
        ["NavAbout"] = "About",

        // Devices Page
        ["HeaderDevices"] = "Devices",
        ["HeaderDevicesDesc"] = "Manage your input devices",
        ["Refresh"] = "Refresh",
        ["ActivityHighlight"] = "Activity Highlight",
        ["ActivityHint"] = "Toggle to highlight active device",
        ["TotalDevices"] = "Total",
        ["All"] = "All",
        ["Keyboards"] = "Keyboards",
        ["Mice"] = "Mice",
        ["Touchpads"] = "Touchpads",
        ["OtherDevices"] = "Other",

        // Device Info
        ["Keyboard"] = "Keyboard",
        ["Mouse"] = "Mouse",
        ["Touchpad"] = "Touchpad",
        ["Unknown"] = "Unknown",
        ["External"] = "External",
        ["BuiltIn"] = "Built-in",
        ["Enabled"] = "Enabled",
        ["Disabled"] = "Disabled",
        ["CollapseOthers"] = "Hide Other Devices",

        // Device List
        ["SearchPlaceholder"] = "Search devices...",
        ["DeviceCount"] = "Total: {0} devices",
        ["SortByName"] = "Name",
        ["SortByType"] = "Type",
        ["SortByStatus"] = "Status",
        ["HasNote"] = "Has note",

        // Statistics
        ["HeaderStatistics"] = "Statistics",
        ["HeaderStatisticsDesc"] = "Device usage overview",
        ["ActiveDevices"] = "Active",
        ["DisabledDevices"] = "Disabled",
        ["ExternalDevices"] = "External",
        ["DeviceDistribution"] = "Device Distribution",

        // Timer
        ["HeaderTimer"] = "Auto Restore",
        ["HeaderTimerDesc"] = "Schedule device re-enabling",
        ["AutoRestoreTimer"] = "Auto Restore Timer",
        ["AutoRestoreDesc"] = "Automatically re-enable all disabled devices after timeout",
        ["EnableAutoRestore"] = "Enable Auto Restore",
        ["TimeoutMinutes"] = "Timeout (minutes)",
        ["QuickPresets"] = "Quick Presets",
        ["DisabledDevicesList"] = "Currently Disabled Devices",
        ["DisabledListDesc"] = "Devices that will be re-enabled:",

        // Settings
        ["HeaderSettings"] = "Settings",
        ["HeaderSettingsDesc"] = "Customize your experience",
        ["Language"] = "Language",
        ["English"] = "English",
        ["Chinese"] = "中文",
        ["Theme"] = "Theme",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["ThemeOcean"] = "Ocean",
        ["ThemeForest"] = "Forest",
        ["ThemeSunset"] = "Sunset",
        ["Appearance"] = "Appearance",
        ["Behavior"] = "Behavior",
        ["AutoStart"] = "Auto-start with Windows",
        ["AutoRefresh"] = "Auto-refresh device list",
        ["ShowNotifications"] = "Show notifications",
        ["SystemTray"] = "System Tray",
        ["MinimizeToTray"] = "Minimize to system tray",
        ["ShowDeviceCount"] = "Show device count in tray",
        ["CloseToTray"] = "Close to tray",

        // Data Management
        ["DataManagement"] = "Data Management",
        ["ClearUsageData"] = "Clear Usage Data",
        ["ClearDataConfirm"] = "This will delete all tracked usage data. Continue?",
        ["DataCleared"] = "Usage data cleared",

        // About section
        ["Version"] = "Version",
        ["AppVersion"] = "1.0.0",

        // About
        ["HeaderAbout"] = "About",
        ["HeaderAboutDesc"] = "Learn more about RubyDevice",
        ["Features"] = "Features",
        ["Feature1"] = "Enumerate all input devices",
        ["Feature2"] = "Enable/disable individual devices",
        ["Feature3"] = "Timer-based auto-restore",
        ["Feature4"] = "Modern WinUI 3 interface",

        // Confirmation Dialog
        ["ConfirmTitle"] = "Confirm Device Disable",
        ["ConfirmMessage"] = "Device will be disabled. This operation will be automatically reverted in {0} seconds if not confirmed.",
        ["ConfirmSave"] = "Save",
        ["ConfirmCancel"] = "Cancel",
        ["SecondsRemaining"] = "seconds remaining",

        // Device Toggle Dialogs
        ["DisableConfirmTitle"] = "Disable Device",
        ["DisableConfirmMessage"] = "This device will be disabled.\nIt will auto-restore in 10 seconds.",
        ["EnableConfirmTitle"] = "Enable Device",
        ["EnableConfirmMessage"] = "Enable this device?",
        ["DisablingIn"] = "Disabling in",
        ["AutoRestoreHint"] = "Auto-restore in 10s after disabled",
        ["WaitingForConfirm"] = "Click Confirm to proceed, Cancel to abort",

        // Auto Restore Dialog
        ["DeviceDisabledTitle"] = "Device Disabled",
        ["DeviceDisabledMessage"] = "{0} is now disabled.",
        ["AutoRestoreCountdown"] = "Auto-restore in",
        ["AutoRestoreHintDialog"] = "Click 'Keep Disabled' to stop auto-restore",
        ["KeepDisabled"] = "Keep Disabled",
        ["RestoreNow"] = "Restore Now",

        // Notifications
        ["DeviceDisabled"] = "Device Disabled",
        ["DeviceEnabled"] = "Device Enabled",
        ["AutoRestoreNotification"] = "{0} has been disabled.\nAuto-restore in {1} seconds...",
        ["DeviceRestored"] = "Device Restored",
        ["DeviceRestoredMessage"] = "{0} has been automatically re-enabled",
        ["NotificationsEnabled"] = "Notifications",

        // Actions
        ["Save"] = "Save",
        ["Cancel"] = "Cancel",
        ["Close"] = "Close",
        ["Minimize"] = "Minimize",
        ["Maximize"] = "Maximize",
        ["Start"] = "Start",
        ["Stop"] = "Stop",
        ["RestoreAll"] = "Restore All",
        ["Minutes"] = "minutes",
        ["TimerRunning"] = "Running...",
        ["TimerIdle"] = "Idle",

        // Usage Record Page
        ["NavUsageRecord"] = "Usage Record",
        ["HeaderUsageRecord"] = "Usage Record",
        ["HeaderUsageRecordDesc"] = "Track device usage time",
        ["TrackingEnabled"] = "Tracking Enabled",
        ["TrackingDisabled"] = "Tracking Disabled",
        ["TrackedDevices"] = "Tracked Devices",
        ["TodayUsage"] = "Today's Usage",
        ["ActiveTime"] = "Active Time",
        ["EnabledTime"] = "Enabled Time",
        ["RetentionDays"] = "Data Retention Days",
        ["AutoCleanup"] = "Auto Cleanup",
        ["NoTrackingDevices"] = "No devices are being tracked",
        ["EnableTrackingHint"] = "Enable tracking on a device to start recording",
        ["Last7Days"] = "Last 7 Days",
        ["Last30Days"] = "Last 30 Days",
        ["AllTime"] = "All Time",
        ["ByWeek"] = "By Week",
        ["ByMonth"] = "By Month",
        ["SelectDevice"] = "Select Device",
        ["UsageHistory"] = "Usage History",
        ["Date"] = "Date",
        ["TrackedSince"] = "Tracked since {0}",
        ["ExportData"] = "Export Data",
        ["ExportSuccess"] = "Data exported successfully",
        ["ExportFailed"] = "Export failed",

        // Device Detail Page
        ["Back"] = "Back",
        ["DeviceInfo"] = "Device Information",
        ["Manufacturer"] = "Manufacturer",
        ["VendorId"] = "Vendor ID",
        ["ProductId"] = "Product ID",
        ["Connection"] = "Connection",
        ["DeviceId"] = "Device ID",
        ["UsageStats"] = "Usage Statistics",
        ["TrackUsage"] = "Track Usage",
        ["Notes"] = "Notes",
        ["AddNoteHint"] = "Add a note for this device...",
    };

    private static readonly Dictionary<string, string> ChineseStrings = new()
    {
        // App
        ["AppName"] = "RubyDevice",
        ["AppDescription"] = "输入设备控制器",

        // Navigation
        ["NavDevices"] = "设备",
        ["NavStatistics"] = "统计",
        ["NavTimer"] = "定时器",
        ["NavSettings"] = "设置",
        ["NavAbout"] = "关于",

        // Devices Page
        ["HeaderDevices"] = "设备管理",
        ["HeaderDevicesDesc"] = "管理您的输入设备",
        ["Refresh"] = "刷新",
        ["ActivityHighlight"] = "活动高亮",
        ["ActivityHint"] = "开启后活动设备会高亮显示",
        ["TotalDevices"] = "总计",
        ["All"] = "全部",
        ["Keyboards"] = "键盘",
        ["Mice"] = "鼠标",
        ["Touchpads"] = "触控板",
        ["OtherDevices"] = "其他",

        // Device Info
        ["Keyboard"] = "键盘",
        ["Mouse"] = "鼠标",
        ["Touchpad"] = "触控板",
        ["Unknown"] = "未知",
        ["External"] = "外接",
        ["BuiltIn"] = "内置",
        ["Enabled"] = "已启用",
        ["Disabled"] = "已禁用",
        ["CollapseOthers"] = "折叠其他设备",

        // Device List
        ["SearchPlaceholder"] = "搜索设备...",
        ["DeviceCount"] = "共 {0} 台设备",
        ["SortByName"] = "名称",
        ["SortByType"] = "类型",
        ["SortByStatus"] = "状态",
        ["HasNote"] = "有备注",

        // Statistics
        ["HeaderStatistics"] = "统计",
        ["HeaderStatisticsDesc"] = "设备使用概览",
        ["ActiveDevices"] = "活动",
        ["DisabledDevices"] = "已禁用",
        ["ExternalDevices"] = "外接",
        ["DeviceDistribution"] = "设备分布",

        // Timer
        ["HeaderTimer"] = "自动恢复",
        ["HeaderTimerDesc"] = "计划设备重新启用",
        ["AutoRestoreTimer"] = "自动恢复计时器",
        ["AutoRestoreDesc"] = "超时后自动重新启用所有已禁用的设备",
        ["EnableAutoRestore"] = "启用自动恢复",
        ["TimeoutMinutes"] = "超时时间（分钟）",
        ["QuickPresets"] = "快速预设",
        ["DisabledDevicesList"] = "当前已禁用的设备",
        ["DisabledListDesc"] = "计时器到期后将重新启用的设备：",

        // Settings
        ["HeaderSettings"] = "设置",
        ["HeaderSettingsDesc"] = "自定义您的体验",
        ["Language"] = "语言",
        ["English"] = "English",
        ["Chinese"] = "中文",
        ["Theme"] = "主题",
        ["ThemeLight"] = "浅色",
        ["ThemeDark"] = "深色",
        ["ThemeOcean"] = "海洋",
        ["ThemeForest"] = "森林",
        ["ThemeSunset"] = "日落",
        ["Appearance"] = "外观",
        ["Behavior"] = "行为",
        ["AutoStart"] = "开机自动启动",
        ["AutoRefresh"] = "自动刷新设备列表",
        ["ShowNotifications"] = "显示通知",
        ["SystemTray"] = "系统托盘",
        ["MinimizeToTray"] = "最小化到系统托盘",
        ["ShowDeviceCount"] = "在托盘显示设备数量",
        ["CloseToTray"] = "关闭到托盘",

        // Data Management
        ["DataManagement"] = "数据管理",
        ["ClearUsageData"] = "清除使用数据",
        ["ClearDataConfirm"] = "这将删除所有追踪的使用数据，确定继续吗？",
        ["DataCleared"] = "使用数据已清除",

        // About section
        ["AppVersion"] = "1.0.0",

        // About
        ["HeaderAbout"] = "关于",
        ["HeaderAboutDesc"] = "了解有关 RubyDevice 的更多信息",
        ["Features"] = "功能",
        ["Feature1"] = "枚举所有输入设备",
        ["Feature2"] = "启用/禁用单个设备",
        ["Feature3"] = "定时自动恢复",
        ["Feature4"] = "现代 WinUI 3 界面",

        // Confirmation Dialog
        ["ConfirmTitle"] = "确认禁用设备",
        ["ConfirmMessage"] = "设备将被禁用。如未确认，操作将在 {0} 秒后自动撤销。",
        ["ConfirmSave"] = "保存",
        ["ConfirmCancel"] = "取消",
        ["SecondsRemaining"] = "秒后撤销",

        // Device Toggle Dialogs
        ["DisableConfirmTitle"] = "禁用设备",
        ["DisableConfirmMessage"] = "此设备将被禁用。\n禁用后将在 10 秒后自动恢复。",
        ["EnableConfirmTitle"] = "启用设备",
        ["EnableConfirmMessage"] = "确定要启用此设备吗？",
        ["DisablingIn"] = "禁用倒计时",
        ["AutoRestoreHint"] = "禁用后 10 秒自动恢复",
        ["WaitingForConfirm"] = "点击确认继续，取消则放弃操作",

        // Auto Restore Dialog
        ["DeviceDisabledTitle"] = "设备已禁用",
        ["DeviceDisabledMessage"] = "{0} 已被禁用。",
        ["AutoRestoreCountdown"] = "自动恢复倒计时",
        ["AutoRestoreHintDialog"] = "点击'保持禁用'可停止自动恢复",
        ["KeepDisabled"] = "保持禁用",
        ["RestoreNow"] = "立即恢复",

        // Notifications
        ["DeviceDisabled"] = "设备已禁用",
        ["DeviceEnabled"] = "设备已启用",
        ["AutoRestoreNotification"] = "{0} 已被禁用。\n{1} 秒后自动恢复...",
        ["DeviceRestored"] = "设备已恢复",
        ["DeviceRestoredMessage"] = "{0} 已自动重新启用",
        ["NotificationsEnabled"] = "显示通知",

        // Actions
        ["Save"] = "保存",
        ["Cancel"] = "取消",
        ["Close"] = "关闭",
        ["Minimize"] = "最小化",
        ["Maximize"] = "最大化",
        ["Start"] = "开始",
        ["Stop"] = "停止",
        ["RestoreAll"] = "全部恢复",
        ["Minutes"] = "分钟",
        ["TimerRunning"] = "运行中...",
        ["TimerIdle"] = "空闲",

        // Usage Record Page
        ["NavUsageRecord"] = "使用记录",
        ["HeaderUsageRecord"] = "使用记录",
        ["HeaderUsageRecordDesc"] = "追踪设备使用时间",
        ["TrackingEnabled"] = "已开启追踪",
        ["TrackingDisabled"] = "未开启追踪",
        ["TrackedDevices"] = "追踪中的设备",
        ["TodayUsage"] = "今日使用",
        ["ActiveTime"] = "活跃时长",
        ["EnabledTime"] = "启用时长",
        ["RetentionDays"] = "数据保留天数",
        ["AutoCleanup"] = "自动清理",
        ["NoTrackingDevices"] = "暂无追踪中的设备",
        ["EnableTrackingHint"] = "开启设备追踪以开始记录",
        ["Last7Days"] = "最近 7 天",
        ["Last30Days"] = "最近 30 天",
        ["AllTime"] = "全部时间",
        ["SelectDevice"] = "选择设备",
        ["UsageHistory"] = "使用历史",
        ["Date"] = "日期",
        ["TrackedSince"] = "自 {0} 开始追踪",
        ["ByWeek"] = "按周",
        ["ByMonth"] = "按月",
        ["ExportData"] = "导出数据",
        ["ExportSuccess"] = "数据导出成功",
        ["ExportFailed"] = "导出失败",

        // Device Detail Page
        ["Back"] = "返回",
        ["DeviceInfo"] = "设备信息",
        ["Manufacturer"] = "制造商",
        ["VendorId"] = "厂商ID",
        ["ProductId"] = "产品ID",
        ["Connection"] = "连接方式",
        ["DeviceId"] = "设备ID",
        ["UsageStats"] = "使用统计",
        ["TrackUsage"] = "追踪使用",
        ["Notes"] = "备注",
        ["AddNoteHint"] = "为此设备添加备注...",
    };

    public string this[string key] => Strings.TryGetValue(key, out var value) ? value : key;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}