using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Throughput.Helpers;

/// <summary>
/// Helper class for keeping windows always on top using Windows API
/// </summary>
public static class WindowHelper
{
    private const int HWND_TOPMOST = -1;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_SHOWNOACTIVATE = 4;
    private const int SW_SHOW = 5;

    /// <summary>
    /// Forces a window to be topmost using Windows API
    /// </summary>
    public static void ForceTopmost(Window window)
    {
        if (window == null) return;
        
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        
        if (hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0, 
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Forces a window to be visible and topmost
    /// </summary>
    public static void ForceVisibleAndTopmost(Window window)
    {
        if (window == null) return;
        
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        
        if (hwnd == IntPtr.Zero) return;

        // Show window without activating
        ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        
        // Set topmost
        SetWindowPos(hwnd, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0, 
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }
}
