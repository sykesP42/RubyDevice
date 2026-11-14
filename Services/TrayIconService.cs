using System;
using System.Runtime.InteropServices;

namespace RubyDevice.Services;

/// <summary>
/// Shows a system tray (notification area) icon with a small context menu.
/// A hidden message-only window receives the icon's callback messages; create,
/// use, and dispose the service on a single (UI) thread.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    // Shell_NotifyIcon
    private const uint NIM_ADD = 0x00, NIM_MODIFY = 0x01, NIM_DELETE = 0x02;
    private const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04;
    private const int TRAY_CALLBACK_MSG = 0x8001; // WM_APP + 1

    // Mouse messages delivered through the callback
    private const int WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONUP = 0x0205;

    private const int WM_NULL = 0x0000;
    private const int HWND_MESSAGE = -3;
    private const uint IMAGE_ICON = 1;
    private const uint LR_DEFAULTSIZE = 0x40, LR_LOADFROMFILE = 0x10;

    // TrackPopupMenu
    private const uint MF_STRING = 0x0000, MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100, TPM_NONOTIFY = 0x0080;
    private const int IDM_OPEN = 1, IDM_EXIT = 2;

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSW
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassW(ref WNDCLASSW lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hInst, string lpName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
        int nReserved, IntPtr hwnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private IntPtr _window = IntPtr.Zero;
    private IntPtr _icon = IntPtr.Zero;
    private uint _classRegistration;
    private WndProcDelegate? _wndProc;   // rooted: the native window holds a raw pointer to it
    private string _tooltip = "RubyDevice";
    private bool _iconVisible;
    private bool _disposed;

    /// <summary>Raised when the user clicks (or double-clicks) the tray icon or picks Open.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the user picks Exit in the tray menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Creates the hidden callback window, loads the icon, and shows it in the
    /// notification area. Safe to call again to re-show after Hide().
    /// </summary>
    public void Initialize(string iconPath, string tooltip)
    {
        if (_disposed) return;

        if (!string.IsNullOrWhiteSpace(tooltip))
            _tooltip = tooltip.Length > 127 ? tooltip[..127] : tooltip;

        if (_window == IntPtr.Zero)
        {
            CreateCallbackWindow();
        }
        if (_icon == IntPtr.Zero)
        {
            _icon = LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_LOADFROMFILE);
        }

        AddIcon();
    }

    /// <summary>Shows the tray icon if it is not currently visible.</summary>
    public void Show() => AddIcon();

    /// <summary>Removes the tray icon (e.g. while the window is visible).</summary>
    public void Hide() => RemoveIcon();

    private void CreateCallbackWindow()
    {
        _wndProc = WndProc;
        var wndClass = new WNDCLASSW
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "RubyDeviceTrayWnd"
        };
        _classRegistration = RegisterClassW(ref wndClass); // failing with "already exists" is fine

        _window = CreateWindowExW(0, wndClass.lpszClassName, "RubyDeviceTray", 0,
            0, 0, 0, 0, (IntPtr)HWND_MESSAGE, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
    }

    private bool AddIcon()
    {
        if (_disposed || _window == IntPtr.Zero || _icon == IntPtr.Zero || _iconVisible)
            return _iconVisible;

        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _window,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = (uint)TRAY_CALLBACK_MSG,
            hIcon = _icon,
            szTip = _tooltip
        };

        _iconVisible = Shell_NotifyIconW(NIM_ADD, ref data);
        return _iconVisible;
    }

    private void RemoveIcon()
    {
        if (!_iconVisible || _window == IntPtr.Zero) return;

        var data = new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _window,
            uID = 1
        };
        Shell_NotifyIconW(NIM_DELETE, ref data);
        _iconVisible = false;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == TRAY_CALLBACK_MSG)
        {
            switch ((int)lParam)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_RBUTTONUP:
                    ShowContextMenu();
                    break;
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (_window == IntPtr.Zero) return;

        var loc = LocalizationService.Instance;
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        AppendMenuW(menu, MF_STRING, (IntPtr)IDM_OPEN, loc["Open"]);
        AppendMenuW(menu, MF_SEPARATOR, IntPtr.Zero, string.Empty);
        AppendMenuW(menu, MF_STRING, (IntPtr)IDM_EXIT, loc["Exit"]);

        try
        {
            GetCursorPos(out var point);
            // The menu needs the foreground window to dismiss on outside clicks.
            SetForegroundWindow(_window);
            int cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
                point.X, point.Y, 0, _window, IntPtr.Zero);
            PostMessageW(_window, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (cmd == IDM_OPEN)
                OpenRequested?.Invoke(this, EventArgs.Empty);
            else if (cmd == IDM_EXIT)
                ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RemoveIcon();

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
        if (_window != IntPtr.Zero)
        {
            DestroyWindow(_window);
            _window = IntPtr.Zero;
        }
    }
}
