using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RubyDevice.Core;

public enum DeviceType { Unknown, Keyboard, Mouse, Touchpad }

public class DeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public DeviceType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsExternal { get; set; }
    public string VendorId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string DevicePath { get; set; } = "";
    public string UserNote { get; set; } = "";
    public long TotalUsageSeconds { get; set; }
}

public class DeviceManager : IDisposable
{
    private const int RIM_TYPEMOUSE = 0, RIM_TYPEKEYBOARD = 1, RIM_TYPEHID = 2;
    private const int RIDI_DEVICENAME = 0x20000007, RIDI_DEVICEINFO = 0x2000000b;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST { public IntPtr hDevice; public int dwType; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO
    {
        public int cbSize, dwType;
        public RID_DEVICE_INFO_HID hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO_HID
    {
        public int dwVendorId, dwProductId, dwVersionNumber, usUsagePage, usUsage;
    }

    // Low-level hook constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    // Raw Input device registration
    private const int RIDEV_INPUTSINK = 0x100;
    private const int WM_INPUT = 0x00FF;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public int dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int ptX;
        public int ptY;
        public int mouseData;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public int dwType;
        public int dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort Flags;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RAWINPUTDATA
    {
        [FieldOffset(0)] public RAWMOUSE mouse;
        [FieldOffset(0)] public RAWKEYBOARD keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWINPUTDATA data;
    }

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint puiNumDevices, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, StringBuilder pData, ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, int cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, int cbSize);

    private static readonly Dictionary<string, string> KnownVendors = new()
    {
        ["046D"] = "Logitech", ["045E"] = "Microsoft", ["06CB"] = "Synaptics",
        ["04F3"] = "Elan", ["17EF"] = "Lenovo", ["1028"] = "Dell", ["0B05"] = "ASUS",
        ["1532"] = "Razer", ["1038"] = "SteelSeries", ["1B1C"] = "Corsair",
        ["046A"] = "Cherry", ["093A"] = "Pixart", ["258A"] = "Rapoo",
        ["1EA7"] = "Redragon", ["04CA"] = "Lite-On"
    };

    public List<DeviceInfo> Devices { get; } = new();
    private readonly Dictionary<string, DeviceInfo> _cache = new();
    private readonly string _dataPath;

    // Hook handles
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private LowLevelHookProc? _hookProc;

    // Device blocking state - stores device handles that should be blocked
    private readonly HashSet<IntPtr> _blockedHandles = new();
    private readonly Dictionary<string, IntPtr> _devicePathToHandle = new();
    private readonly object _blockLock = new();

    // Current input device handle (updated by Raw Input)
    private IntPtr _currentInputDevice = IntPtr.Zero;
    private IntPtr _targetWindow = IntPtr.Zero;

    public DeviceManager()
    {
        _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RubyDevice", "device_data.json");
        LoadCache();
    }

    private void LoadCache()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                var data = JsonSerializer.Deserialize<Dictionary<string, DeviceInfo>>(json);
                if (data != null) foreach (var kv in data) _cache[kv.Key] = kv.Value;
            }
        }
        catch { }
    }

    /// <summary>
    /// Register a window to receive raw input and track device handles
    /// </summary>
    public void RegisterRawInput(IntPtr windowHandle)
    {
        _targetWindow = windowHandle;

        // Register for keyboard and mouse raw input
        RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[2];

        // Keyboard: UsagePage 1, Usage 6
        devices[0].usUsagePage = 0x01;
        devices[0].usUsage = 0x06;
        devices[0].dwFlags = RIDEV_INPUTSINK;
        devices[0].hwndTarget = windowHandle;

        // Mouse: UsagePage 1, Usage 2
        devices[1].usUsagePage = 0x01;
        devices[1].usUsage = 0x02;
        devices[1].dwFlags = RIDEV_INPUTSINK;
        devices[1].hwndTarget = windowHandle;

        RegisterRawInputDevices(devices, 2, Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    /// <summary>
    /// Process WM_INPUT message to track which device generated the input
    /// </summary>
    public void ProcessRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        GetRawInputData(hRawInput, 0x10000003, IntPtr.Zero, ref size, Marshal.SizeOf<RAWINPUTHEADER>());

        if (size > 0)
        {
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRawInput, 0x10000003, buffer, ref size, Marshal.SizeOf<RAWINPUTHEADER>()) > 0)
                {
                    RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                    _currentInputDevice = raw.header.hDevice;
                    // Notify tracking service of active input
                    Services.UsageTrackingService.Instance.ProcessActiveInput(raw.header.hDevice);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    public void RefreshDevices()
    {
        Devices.Clear();
        _devicePathToHandle.Clear();

        // Clear stale handle mappings before re-enumerating devices
        Services.UsageTrackingService.Instance.ClearDeviceHandles();

        try
        {
            uint count = 0;
            int size = Marshal.SizeOf<RAWINPUTDEVICELIST>();
            GetRawInputDeviceList(IntPtr.Zero, ref count, size);
            if (count == 0) { AddFallback(); return; }

            var list = new RAWINPUTDEVICELIST[count];
            var ptr = Marshal.AllocHGlobal((int)(count * size));
            try
            {
                GetRawInputDeviceList(ptr, ref count, size);
                for (int i = 0; i < count; i++)
                    list[i] = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(ptr + i * size);

                foreach (var dev in list)
                {
                    var info = GetDeviceInfo(dev.hDevice, dev.dwType);
                    if (info != null && !string.IsNullOrEmpty(info.Name))
                    {
                        // Store device path to handle mapping for blocking
                        _devicePathToHandle[info.DevicePath] = dev.hDevice;

                        if (_cache.TryGetValue(info.DeviceId, out var cached))
                        {
                            info.UserNote = cached.UserNote;
                            info.TotalUsageSeconds = cached.TotalUsageSeconds;
                        }
                        // Register device handle with tracking service
                        Services.UsageTrackingService.Instance.RegisterDeviceHandle(dev.hDevice, info.DeviceId);
                        Devices.Add(info);
                    }
                }
            }
            finally { Marshal.FreeHGlobal(ptr); }

            if (Devices.Count == 0) AddFallback();
        }
        catch { AddFallback(); }
    }

    private DeviceInfo? GetDeviceInfo(IntPtr hDevice, int dwType)
    {
        var dev = new DeviceInfo { DeviceId = hDevice.ToString(), IsEnabled = true };

        uint nameSize = 0;
        GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref nameSize);
        if (nameSize > 0)
        {
            var sb = new StringBuilder((int)nameSize);
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, sb, ref nameSize);
            string devicePath = sb.ToString();
            dev.DevicePath = devicePath;
            dev.VendorId = ExtractVidPid(devicePath, "VID_");
            dev.ProductId = ExtractVidPid(devicePath, "PID_");
            dev.IsExternal = !devicePath.ToUpperInvariant().Contains("ROOT");

            var path = devicePath.ToUpperInvariant();
            if (path.Contains("PRECISION") || path.Contains("TOUCHPAD") || path.Contains("TRACKPAD") ||
                path.Contains("DIGITIZER") || path.Contains("SYNAPTICS") || path.Contains("ELAN") ||
                path.Contains("ALPS") || dev.VendorId == "06CB" || dev.VendorId == "04F3")
            {
                dev.Type = DeviceType.Touchpad;
            }
        }

        var info = new RID_DEVICE_INFO { cbSize = Marshal.SizeOf<RID_DEVICE_INFO>() };
        var pInfo = Marshal.AllocHGlobal(info.cbSize);
        try
        {
            Marshal.StructureToPtr(info, pInfo, false);
            uint sz = (uint)info.cbSize;
            if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, pInfo, ref sz) > 0)
            {
                info = Marshal.PtrToStructure<RID_DEVICE_INFO>(pInfo);

                if (dev.Type == DeviceType.Unknown)
                {
                    if (info.hid.usUsagePage == 0x01)
                    {
                        dev.Type = info.hid.usUsage switch
                        {
                            0x06 => DeviceType.Keyboard,
                            0x02 => DeviceType.Mouse,
                            _ => dwType switch
                            {
                                RIM_TYPEKEYBOARD => DeviceType.Keyboard,
                                RIM_TYPEMOUSE => DeviceType.Mouse,
                                _ => DeviceType.Unknown
                            }
                        };
                    }
                    else if (info.hid.usUsagePage == 0x0D)
                    {
                        dev.Type = DeviceType.Touchpad;
                    }
                    else
                    {
                        dev.Type = dwType switch
                        {
                            RIM_TYPEKEYBOARD => DeviceType.Keyboard,
                            RIM_TYPEMOUSE => DeviceType.Mouse,
                            _ => DeviceType.Unknown
                        };
                    }
                }

                if (string.IsNullOrEmpty(dev.VendorId))
                    dev.VendorId = info.hid.dwVendorId.ToString("X4");
                if (string.IsNullOrEmpty(dev.ProductId))
                    dev.ProductId = info.hid.dwProductId.ToString("X4");
            }
        }
        finally { Marshal.FreeHGlobal(pInfo); }

        if (KnownVendors.TryGetValue(dev.VendorId, out var mfr))
            dev.Manufacturer = mfr;

        dev.Name = dev.Type switch
        {
            DeviceType.Touchpad => !string.IsNullOrEmpty(dev.Manufacturer) ? $"{dev.Manufacturer} Touchpad" : "Precision Touchpad",
            DeviceType.Keyboard => !string.IsNullOrEmpty(dev.Manufacturer) ? $"{dev.Manufacturer} Keyboard" : "Keyboard",
            DeviceType.Mouse => !string.IsNullOrEmpty(dev.Manufacturer) ? $"{dev.Manufacturer} Mouse" : "Mouse",
            _ => !string.IsNullOrEmpty(dev.Manufacturer) ? $"{dev.Manufacturer} Device" : "HID Device"
        };

        return dev;
    }

    private static string ExtractVidPid(string path, string prefix)
    {
        int idx = path.ToUpperInvariant().IndexOf(prefix);
        return idx >= 0 && idx + 7 <= path.Length ? path.Substring(idx + 4, 4) : "";
    }

    private void AddFallback()
    {
        Devices.Add(new DeviceInfo { Name = "Keyboard", Type = DeviceType.Keyboard, IsEnabled = true });
        Devices.Add(new DeviceInfo { Name = "Mouse", Type = DeviceType.Mouse, IsEnabled = true });
        Devices.Add(new DeviceInfo { Name = "Touchpad", Type = DeviceType.Touchpad, IsEnabled = true });
    }

    /// <summary>
    /// Install low-level hooks (called automatically when blocking is needed)
    /// </summary>
    public void InstallHooks()
    {
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            _hookProc = HookProc;
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);
        }
    }

    /// <summary>
    /// Remove low-level hooks
    /// </summary>
    public void UninstallHooks()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Combined hook procedure for both keyboard and mouse
    /// </summary>
    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            // Check if current input device is blocked
            bool shouldBlock = false;
            lock (_blockLock)
            {
                shouldBlock = _blockedHandles.Contains(_currentInputDevice);
            }

            if (shouldBlock)
            {
                // Block this input by returning non-zero
                return new IntPtr(1);
            }
        }

        // Determine which hook to call next
        IntPtr hookToCall = _keyboardHook;
        int msg = wParam.ToInt32();
        if (msg >= 0x0200 && msg <= 0x020E) // Mouse messages range
        {
            hookToCall = _mouseHook;
        }

        return CallNextHookEx(hookToCall, nCode, wParam, lParam);
    }

    /// <summary>
    /// Toggle device enabled state using low-level hooks (no admin required)
    /// </summary>
    public bool ToggleDevice(string deviceId, bool enable)
    {
        var dev = Devices.Find(d => d.DeviceId == deviceId);
        if (dev == null) return false;

        // Get the device handle from path
        IntPtr deviceHandle = IntPtr.Zero;
        if (_devicePathToHandle.TryGetValue(dev.DevicePath, out var handle))
        {
            deviceHandle = handle;
        }
        else
        {
            // Try parsing the deviceId as a handle
            if (long.TryParse(deviceId, out long handleValue))
            {
                deviceHandle = new IntPtr(handleValue);
            }
        }

        if (enable)
        {
            // Enable device - remove from blocked list
            lock (_blockLock)
            {
                _blockedHandles.Remove(deviceHandle);
            }
            dev.IsEnabled = true;

            // If nothing is blocked, uninstall hooks
            if (_blockedHandles.Count == 0)
            {
                UninstallHooks();
            }
        }
        else
        {
            // Disable device - add to blocked list and install hooks
            InstallHooks();
            lock (_blockLock)
            {
                _blockedHandles.Add(deviceHandle);
            }
            dev.IsEnabled = false;
        }

        return true;
    }

    public void Dispose()
    {
        UninstallHooks();
    }
}