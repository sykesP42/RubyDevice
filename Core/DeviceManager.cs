using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RubyDevice.Core;

/// <summary>
/// Classification of an input device by the kind of input it produces
/// </summary>
public enum DeviceType { Unknown, Keyboard, Mouse, Touchpad }

/// <summary>
/// Core device descriptor returned by DeviceManager
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Stable identifier derived from the device path (VID/PID or ACPI id).
    /// Unlike raw input handles, it survives reboots and re-enumeration.
    /// </summary>
    public string DeviceId { get; set; } = "";
    /// <summary>Display name of the device</summary>
    public string Name { get; set; } = "";
    /// <summary>Manufacturer name (resolved from known vendor list)</summary>
    public string Manufacturer { get; set; } = "";
    /// <summary>Device type classification</summary>
    public DeviceType Type { get; set; }
    /// <summary>Whether the device is currently enabled</summary>
    public bool IsEnabled { get; set; } = true;
    /// <summary>True for external (non-built-in) devices</summary>
    public bool IsExternal { get; set; }
    /// <summary>USB vendor ID as hex string</summary>
    public string VendorId { get; set; } = "";
    /// <summary>USB product ID as hex string</summary>
    public string ProductId { get; set; } = "";
    /// <summary>Raw input device path for handle lookup</summary>
    public string DevicePath { get; set; } = "";
    /// <summary>User-entered note for this device</summary>
    public string UserNote { get; set; } = "";
    /// <summary>Total usage time in seconds</summary>
    public long TotalUsageSeconds { get; set; }
}

/// <summary>
/// Enumerates input devices via the Windows Raw Input API and blocks input from
/// disabled devices using low-level keyboard and mouse hooks.
///
/// Blocking works by remembering which device produced the most recent raw input
/// of each type (keyboard/mouse are tracked independently) and swallowing hook
/// events while that device is blocked. Because input swallowed by a hook never
/// generates WM_INPUT, the last-known device can go stale; holding both Shift
/// keys together is an emergency escape hatch that unblocks everything.
/// </summary>
public class DeviceManager : IDisposable
{
    private const int RIM_TYPEMOUSE = 0, RIM_TYPEKEYBOARD = 1, RIM_TYPEHID = 2;
    private const int RIDI_DEVICENAME = 0x20000007, RIDI_DEVICEINFO = 0x2000000b;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST { public IntPtr hDevice; public int dwType; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO_MOUSE
    {
        public int dwId, dwNumberOfButtons, dwSampleRate;
        public bool fHasHorizontalWheel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO_KEYBOARD
    {
        public int dwType, dwSubType, dwKeyboardMode, dwNumberOfFunctionKeys, dwNumberOfIndicators, dwNumberOfKeysTotal;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO_HID
    {
        public int dwVendorId, dwProductId, dwVersionNumber;
        public ushort usUsagePage, usUsage;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RID_DEVICE_INFO_UNION
    {
        [FieldOffset(0)] public RID_DEVICE_INFO_MOUSE mouse;
        [FieldOffset(0)] public RID_DEVICE_INFO_KEYBOARD keyboard;
        [FieldOffset(0)] public RID_DEVICE_INFO_HID hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO
    {
        public int cbSize, dwType;
        public RID_DEVICE_INFO_UNION u;
    }

    // Low-level hook constants
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;

    // Raw Input device registration
    private const int RIDEV_INPUTSINK = 0x100;
    private const int WM_INPUT = 0x00FF;

    // Virtual-key codes used by the emergency unlock chord (both Shifts held)
    private const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

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
    private struct RAWINPUTHEADER
    {
        public int dwType;
        public int dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
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

    /// <summary>
    /// Currently enumerated devices, grouped by physical device identity.
    /// Guarded by <see cref="_stateLock"/>; callers should use
    /// <see cref="GetDevicesSnapshot"/> to iterate safely from other threads.
    /// </summary>
    public List<DeviceInfo> Devices { get; } = new();
    private readonly object _stateLock = new();
    private readonly Dictionary<string, DeviceInfo> _cache = new();
    private readonly string _dataPath;

    // Hook handles. _hookProc must stay rooted for the process lifetime - the
    // native hook keeps a raw function pointer to the delegate, so if it were
    // collected the next input event would call into freed memory.
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private LowLevelHookProc? _hookProc;

    // Device blocking state - stores device handles that should be blocked
    private readonly HashSet<IntPtr> _blockedHandles = new();
    private readonly Dictionary<string, IntPtr> _devicePathToHandle = new();
    // Store all handles for each physical device (for blocking all interfaces)
    private readonly Dictionary<string, HashSet<IntPtr>> _deviceIdToHandles = new();
    // Device IDs the user disabled; survives re-enumeration within the session
    private readonly HashSet<string> _disabledDeviceIds = new();

    // Device activity tracking
    /// <summary>
    /// Raised when input is detected from a device, throttled to once per 100ms per device
    /// </summary>
    public event EventHandler<DeviceActivityEventArgs>? DeviceActivity;
    private readonly Dictionary<string, DateTime> _lastActivityByDevice = new();

    /// <summary>
    /// Raised when the user triggers the emergency unlock (both Shift keys held).
    /// All devices have been unblocked when this fires.
    /// </summary>
    public event EventHandler? DevicesEmergencyUnlocked;

    /// <summary>
    /// Describes a single input activity notification for a device
    /// </summary>
    public class DeviceActivityEventArgs : EventArgs
    {
        /// <summary>Identifier of the device that produced input</summary>
        public string DeviceId { get; init; } = "";
        /// <summary>Type of the device that produced input</summary>
        public DeviceType DeviceType { get; init; }
        /// <summary>When the input was observed</summary>
        public DateTime Timestamp { get; init; }
    }
    private readonly object _blockLock = new();

    // Last raw-input handle seen for each device type. Kept separately so that
    // blocking a keyboard can never freeze mice and vice versa.
    private IntPtr _lastKeyboardDevice = IntPtr.Zero;
    private IntPtr _lastMouseDevice = IntPtr.Zero;
    private bool _lShiftDown, _rShiftDown;
    private IntPtr _targetWindow = IntPtr.Zero;

    /// <summary>
    /// Initializes a new manager and loads the cached device metadata
    /// </summary>
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
    /// Persist cached device notes. Only the note text is stored; usage totals
    /// live in the usage tracking service's own data file.
    /// </summary>
    public void SaveCache()
    {
        try
        {
            var dir = Path.GetDirectoryName(_dataPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Dictionary<string, DeviceInfo> snapshot;
            lock (_stateLock)
            {
                snapshot = Devices.ToDictionary(d => d.DeviceId, d => d);
            }
            var data = snapshot.ToDictionary(kv => kv.Key, kv => new { kv.Value.UserNote });
            File.WriteAllText(_dataPath, JsonSerializer.Serialize(data));
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
    /// Process WM_INPUT message to track which device generated the input.
    /// Only the RAWINPUTHEADER is decoded - it carries the device handle and type,
    /// and unlike the full struct it never reads past the buffer returned by
    /// GetRawInputData (which can be shorter than the declared RAWINPUT size).
    /// </summary>
    public void ProcessRawInput(IntPtr hRawInput)
    {
        uint size = 0;
        GetRawInputData(hRawInput, 0x10000003, IntPtr.Zero, ref size, Marshal.SizeOf<RAWINPUTHEADER>());

        if (size == 0) return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(hRawInput, 0x10000003, buffer, ref size, Marshal.SizeOf<RAWINPUTHEADER>()) > 0)
            {
                RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                var deviceType = header.dwType == RIM_TYPEKEYBOARD ? DeviceType.Keyboard
                    : header.dwType == RIM_TYPEMOUSE ? DeviceType.Mouse
                    : DeviceType.Unknown;

                lock (_blockLock)
                {
                    if (deviceType == DeviceType.Keyboard) _lastKeyboardDevice = header.hDevice;
                    else if (deviceType == DeviceType.Mouse) _lastMouseDevice = header.hDevice;
                }

                OnDeviceActivity(header.hDevice, deviceType);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Fire device activity event (throttled to max once per 100ms per device)
    /// </summary>
    private void OnDeviceActivity(IntPtr deviceHandle, DeviceType deviceType)
    {
        string? deviceId = GetDeviceIdFromHandle(deviceHandle);
        if (deviceId == null) return;

        // Throttle: only fire if different device or enough time passed
        var now = DateTime.Now;
        lock (_stateLock)
        {
            if (_lastActivityByDevice.TryGetValue(deviceId, out var last) &&
                (now - last).TotalMilliseconds <= 100)
            {
                return;
            }
            _lastActivityByDevice[deviceId] = now;
        }

        DeviceActivity?.Invoke(this, new DeviceActivityEventArgs
        {
            DeviceId = deviceId,
            DeviceType = deviceType,
            Timestamp = now
        });
    }

    /// <summary>
    /// Get the device ID from a handle
    /// </summary>
    public string? GetDeviceIdFromHandle(IntPtr handle)
    {
        lock (_stateLock)
        {
            foreach (var kvp in _deviceIdToHandles)
            {
                if (kvp.Value.Contains(handle))
                    return kvp.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns a thread-safe copy of the currently enumerated devices
    /// </summary>
    public List<DeviceInfo> GetDevicesSnapshot()
    {
        lock (_stateLock)
        {
            return Devices.ToList();
        }
    }

    /// <summary>
    /// Re-enumerates input devices via Raw Input, merging interfaces of the same
    /// physical device and restoring cached notes and enabled/disabled state
    /// </summary>
    public void RefreshDevices()
    {
        lock (_stateLock)
        {
            Devices.Clear();
            _devicePathToHandle.Clear();
            _deviceIdToHandles.Clear();

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

                    // Group devices by physical device identity (VID+PID or path prefix)
                    var deviceGroups = new Dictionary<string, List<(DeviceInfo info, IntPtr handle, int dwType)>>();

                    foreach (var dev in list)
                    {
                        var info = GetDeviceInfo(dev.hDevice, dev.dwType);
                        if (info != null && !string.IsNullOrEmpty(info.Name))
                        {
                            if (!deviceGroups.ContainsKey(info.DeviceId))
                                deviceGroups[info.DeviceId] = new List<(DeviceInfo, IntPtr, int)>();

                            deviceGroups[info.DeviceId].Add((info, dev.hDevice, dev.dwType));
                        }
                    }

                    // Merge grouped devices - select the best representative for each physical device
                    foreach (var group in deviceGroups.Values)
                    {
                        var best = SelectBestDevice(group);
                        if (best.info == null) continue;

                        _devicePathToHandle[best.info.DevicePath] = best.handle;

                        if (_cache.TryGetValue(best.info.DeviceId, out var cached))
                            best.info.UserNote = cached.UserNote;

                        // Restore the enabled/disabled state from this session
                        best.info.IsEnabled = !_disabledDeviceIds.Contains(best.info.DeviceId);

                        // Store all handles for this physical device (for blocking all interfaces)
                        _deviceIdToHandles[best.info.DeviceId] = group
                            .Select(g => g.handle)
                            .ToHashSet();

                        Devices.Add(best.info);
                    }
                }
                finally { Marshal.FreeHGlobal(ptr); }

                if (Devices.Count == 0) AddFallback();
            }
            catch { AddFallback(); }
        }
    }

    /// <summary>
    /// Compute the stable physical identity for a device from its interface path.
    /// Used both for grouping composite interfaces and as the persistent DeviceId.
    /// </summary>
    private static string GetPhysicalDeviceKey(DeviceInfo info)
    {
        var path = info.DevicePath.ToUpperInvariant();

        // If device has VID+PID in path, use it as the primary key
        // This handles composite devices like mice with keyboard interfaces
        var vidIdx = path.IndexOf("VID_");
        var pidIdx = path.IndexOf("PID_");
        if (vidIdx >= 0 && pidIdx > vidIdx && vidIdx + 8 <= path.Length && pidIdx + 8 <= path.Length)
        {
            var vid = path.Substring(vidIdx + 4, 4);
            var pid = path.Substring(pidIdx + 4, 4);
            return $"VIDPID_{vid}_{pid}";
        }

        // Handle internal touchpad - both Microsoft HID RID and UNIW0001 are the same device
        if (path.Contains("MICROSOFT HID RID") || path.Contains("UNIW0001"))
        {
            return "INTERNAL_TOUCHPAD";
        }

        // Handle ACPI built-in devices (keyboard, etc.)
        if (path.Contains("ACPI#"))
        {
            var start = path.IndexOf("ACPI#");
            // Find the device ID part (between ACPI# and next #)
            var end = path.IndexOf('#', start + 5);
            if (end > start + 5)
                return path.Substring(start, end - start);
        }

        // Fallback: use path before &Col or last # segment
        var colIdx = path.IndexOf("&COL");
        if (colIdx > 0)
            return path.Substring(0, colIdx);

        return path;
    }

    /// <summary>
    /// Select the best device interface from a group of interfaces for the same physical device.
    /// For composite devices (mouse + keyboard interfaces), choose based on primary function.
    /// </summary>
    private static (DeviceInfo? info, IntPtr handle) SelectBestDevice(List<(DeviceInfo info, IntPtr handle, int dwType)> group)
    {
        if (group.Count == 0)
            return (null, IntPtr.Zero);

        if (group.Count == 1)
            return (group[0].info, group[0].handle);

        // Count types in the group
        var touchpadCount = group.Count(g => g.info.Type == DeviceType.Touchpad);
        var keyboardCount = group.Count(g => g.info.Type == DeviceType.Keyboard || g.dwType == RIM_TYPEKEYBOARD);
        var mouseCount = group.Count(g => g.info.Type == DeviceType.Mouse || g.dwType == RIM_TYPEMOUSE);

        // Determine primary device type based on majority
        DeviceType primaryType;
        if (touchpadCount > 0)
            primaryType = DeviceType.Touchpad;
        else if (mouseCount >= keyboardCount)
            primaryType = DeviceType.Mouse;  // For mouse+keyboard combo, prefer mouse
        else
            primaryType = DeviceType.Keyboard;

        // Select device with matching type
        var matching = group.Where(g =>
            (primaryType == DeviceType.Touchpad && g.info.Type == DeviceType.Touchpad) ||
            (primaryType == DeviceType.Keyboard && (g.info.Type == DeviceType.Keyboard || g.dwType == RIM_TYPEKEYBOARD)) ||
            (primaryType == DeviceType.Mouse && (g.info.Type == DeviceType.Mouse || g.dwType == RIM_TYPEMOUSE))
        ).ToList();

        if (matching.Count > 0)
        {
            var best = matching.First();
            // Update the name based on primary type if needed
            best.info.Name = GetDeviceName(best.info, primaryType);
            return (best.info, best.handle);
        }

        // Fallback to first device
        return (group[0].info, group[0].handle);
    }

    private static string GetDeviceName(DeviceInfo info, DeviceType type)
    {
        return type switch
        {
            DeviceType.Touchpad => !string.IsNullOrEmpty(info.Manufacturer) ? $"{info.Manufacturer} Touchpad" : "Precision Touchpad",
            DeviceType.Keyboard => !string.IsNullOrEmpty(info.Manufacturer) ? $"{info.Manufacturer} Keyboard" : "Keyboard",
            DeviceType.Mouse => !string.IsNullOrEmpty(info.Manufacturer) ? $"{info.Manufacturer} Mouse" : "Mouse",
            _ => info.Name
        };
    }

    private DeviceInfo? GetDeviceInfo(IntPtr hDevice, int dwType)
    {
        var dev = new DeviceInfo { IsEnabled = true };

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

            // Determine if device is external
            // ACPI devices are built-in, HID devices with VID/PID are typically external
            var path = devicePath.ToUpperInvariant();
            dev.IsExternal = !path.Contains("ACPI#") && !path.Contains("ROOT");

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
                    if (info.u.hid.usUsagePage == 0x01)
                    {
                        dev.Type = info.u.hid.usUsage switch
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
                    else if (info.u.hid.usUsagePage == 0x0D)
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
                    dev.VendorId = info.u.hid.dwVendorId.ToString("X4");
                if (string.IsNullOrEmpty(dev.ProductId))
                    dev.ProductId = info.u.hid.dwProductId.ToString("X4");
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

        // Stable identity: derived from the device path so it survives reboots.
        // Raw input handles would change every session and orphan all persisted data.
        dev.DeviceId = GetPhysicalDeviceKey(dev);
        if (string.IsNullOrEmpty(dev.DeviceId))
            dev.DeviceId = $"UNKNOWN_{hDevice}";

        return dev;
    }

    private static string ExtractVidPid(string path, string prefix)
    {
        // "VID_XXXX" needs 8 characters from the match start
        int idx = path.ToUpperInvariant().IndexOf(prefix);
        return idx >= 0 && idx + 8 <= path.Length ? path.Substring(idx + 4, 4) : "";
    }

    private void AddFallback()
    {
        Devices.Add(new DeviceInfo { Name = "Keyboard", DeviceId = "FALLBACK_KEYBOARD", Type = DeviceType.Keyboard, IsEnabled = true });
        Devices.Add(new DeviceInfo { Name = "Mouse", DeviceId = "FALLBACK_MOUSE", Type = DeviceType.Mouse, IsEnabled = true });
        Devices.Add(new DeviceInfo { Name = "Touchpad", DeviceId = "FALLBACK_TOUCHPAD", Type = DeviceType.Touchpad, IsEnabled = true });
    }

    /// <summary>
    /// Install low-level hooks (called automatically when blocking is needed)
    /// </summary>
    /// <returns>True if both hooks are installed</returns>
    public bool InstallHooks()
    {
        _hookProc ??= HookProc;
        if (_keyboardHook == IntPtr.Zero)
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
        if (_mouseHook == IntPtr.Zero)
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);

        return _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;
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
    /// Combined hook procedure for both keyboard and mouse. Input is blocked only
    /// when the most recent raw input of the SAME type came from a blocked device.
    /// Holding both Shift keys forces every device to be unblocked immediately.
    /// </summary>
    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            bool isMouse = msg >= 0x0200;

            if (!isMouse)
            {
                // Track the emergency unlock chord (both Shift keys) even while
                // blocking, because we still see events we are about to swallow.
                if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (kb.vkCode == VK_LSHIFT) _lShiftDown = true;
                    else if (kb.vkCode == VK_RSHIFT) _rShiftDown = true;
                }
                else if (msg is WM_KEYUP or WM_SYSKEYUP)
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (kb.vkCode == VK_LSHIFT) _lShiftDown = false;
                    else if (kb.vkCode == VK_RSHIFT) _rShiftDown = false;
                }
            }

            bool shouldBlock;
            bool emergency = false;
            lock (_blockLock)
            {
                shouldBlock = _blockedHandles.Contains(isMouse ? _lastMouseDevice : _lastKeyboardDevice);
                if (shouldBlock && _lShiftDown && _rShiftDown)
                {
                    // Emergency escape: unblock everything instead of swallowing
                    // input we may be misattributing.
                    shouldBlock = false;
                    emergency = true;
                }
            }

            if (emergency)
            {
                EmergencyUnlock();
            }

            if (shouldBlock)
            {
                return new IntPtr(1);
            }
        }

        // Route to the matching hook chain; the OS ignores the hook handle for
        // low-level hooks, but pass the right one anyway.
        bool mouseMsg = wParam.ToInt32() >= 0x0200;
        return CallNextHookEx(mouseMsg ? _mouseHook : _keyboardHook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Clears all blocking state after the emergency chord was detected and
    /// notifies subscribers. Hooks are unhooked from here rather than inside the
    /// hook callback, which the system does not guarantee to be reentrant.
    /// </summary>
    private void EmergencyUnlock()
    {
        lock (_blockLock)
        {
            if (_blockedHandles.Count == 0) return;
            _blockedHandles.Clear();
        }

        lock (_stateLock)
        {
            _disabledDeviceIds.Clear();
            foreach (var d in Devices) d.IsEnabled = true;
        }
        DevicesEmergencyUnlocked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggle device enabled state using low-level hooks (no admin required).
    /// When disabling, adds all interfaces of the device to the blocked set.
    /// </summary>
    /// <param name="deviceId">Physical device identifier</param>
    /// <param name="enable">True to enable, false to disable</param>
    /// <returns>True if the device was found and toggled</returns>
    public bool ToggleDevice(string deviceId, bool enable)
    {
        DeviceInfo? dev;
        lock (_stateLock)
        {
            dev = Devices.Find(d => d.DeviceId == deviceId);
        }
        if (dev == null) return false;

        // Get all handles for this physical device (to block all interfaces)
        HashSet<IntPtr>? handles;
        lock (_stateLock)
        {
            _deviceIdToHandles.TryGetValue(deviceId, out handles);
        }

        if (handles == null || handles.Count == 0)
        {
            // Fallback: try to get handle from path
            lock (_stateLock)
            {
                if (_devicePathToHandle.TryGetValue(dev.DevicePath, out var handle))
                    handles = new HashSet<IntPtr> { handle };
            }
            if (handles == null || handles.Count == 0)
                return false;
        }

        if (enable)
        {
            // Enable device - remove all handles from blocked list
            lock (_blockLock)
            {
                foreach (var h in handles)
                    _blockedHandles.Remove(h);
            }
            if (_blockedHandles.Count == 0)
            {
                UninstallHooks();
            }

            lock (_stateLock)
            {
                _disabledDeviceIds.Remove(deviceId);
                dev.IsEnabled = true;
            }
        }
        else
        {
            // Disable device - install hooks first so no input slips through
            if (!InstallHooks())
            {
                return false;
            }

            lock (_blockLock)
            {
                foreach (var h in handles)
                    _blockedHandles.Add(h);
            }

            lock (_stateLock)
            {
                _disabledDeviceIds.Add(deviceId);
                dev.IsEnabled = false;
            }
        }

        return true;
    }

    /// <summary>
    /// Releases unmanaged resources: unhooks the low-level keyboard/mouse hooks
    /// </summary>
    public void Dispose()
    {
        UninstallHooks();
    }
}
