using System;
using System.Runtime.InteropServices;

namespace RubyDevice.Core;

/// <summary>
/// Helper class for window subclassing to receive WM_INPUT messages
/// </summary>
public static class WindowSubclassHelper
{
    private const int GWLP_WNDPROC = -4;
    private const int WM_INPUT = 0x00FF;

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr SubclassProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProcDelegate pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProcDelegate pfnSubclass, IntPtr uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static SubclassProcDelegate? _subclassProc;
    private static Action<IntPtr>? _onRawInput;

    /// <summary>
    /// Install a window subclass to receive WM_INPUT messages
    /// </summary>
    public static bool InstallSubclass(IntPtr hwnd, Action<IntPtr> onRawInput)
    {
        _onRawInput = onRawInput;
        _subclassProc = SubclassProc;

        return SetWindowSubclass(hwnd, _subclassProc, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Remove the window subclass
    /// </summary>
    public static bool UninstallSubclass(IntPtr hwnd)
    {
        if (_subclassProc != null)
        {
            return RemoveWindowSubclass(hwnd, _subclassProc, IntPtr.Zero);
        }
        return false;
    }

    private static IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (msg == WM_INPUT)
        {
            _onRawInput?.Invoke(lParam);
        }

        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}