using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Throughput.Helpers;

/// <summary>
/// Applies the Windows 11 DWM acrylic (frosted-glass) system backdrop with
/// rounded corners to a WPF window. Requires <c>AllowsTransparency="False"</c>;
/// the window's composition target is punched transparent so the DWM backdrop
/// shows through wherever WPF content is translucent.
/// </summary>
internal static class AcrylicGlass
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    /// <summary>
    /// Enables the acrylic backdrop, dark frosting and rounded corners. Call from
    /// <c>OnSourceInitialized</c> once the native handle exists.
    /// </summary>
    public static void Enable(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero) return;

        // Let the DWM backdrop bleed through the WPF client area.
        if (PresentationSource.FromVisual(window) is HwndSource { CompositionTarget: { } target })
        {
            target.BackgroundColor = Colors.Transparent;
        }

        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}
