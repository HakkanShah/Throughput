using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Throughput.Helpers;

/// <summary>
/// Keeps a borderless widget reliably above the taskbar.
///
/// A plain <c>Topmost=True</c> window still gets covered when the Windows shell
/// (the taskbar / Start) comes to the front of the topmost band - e.g. clicking
/// empty taskbar space or launching another app. The widget never had focus in
/// that case, so <see cref="Window.Deactivated"/> doesn't fire and nothing
/// re-raises it.
///
/// Two complementary mechanisms keep it on top:
/// <list type="bullet">
///   <item>An OS foreground-change hook (<c>EVENT_SYSTEM_FOREGROUND</c>) that
///   re-raises the widget the instant another window comes forward - covers app
///   launches, alt-tab, and clicking taskbar buttons.</item>
///   <item>A short polling timer that re-asserts topmost a few times a second -
///   covers cases the hook never hears about, most notably clicking *empty*
///   taskbar space (which doesn't change the foreground window, so no event
///   fires). Re-asserting is a no-op when already on top, so it's effectively
///   free and never flickers.</item>
/// </list>
/// </summary>
public sealed class TopmostGuard : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private const int HWND_TOPMOST = -1;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private readonly Window _window;
    // Held in a field so the GC can't collect the delegate while the OS still
    // holds a native pointer to it (would otherwise crash on the next callback).
    private readonly WinEventDelegate _callback;
    private readonly DispatcherTimer _pollTimer;
    private IntPtr _hook;
    private bool _disposed;

    public TopmostGuard(Window window)
    {
        _window = window;
        _callback = OnForegroundChanged;

        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback,
            idProcess: 0, idThread: 0, WINEVENT_OUTOFCONTEXT);

        // Catches the cases the foreground hook can't hear - chiefly clicking
        // empty taskbar space. 250ms is below the threshold where a brief cover
        // is noticeable, and re-asserting when already topmost costs nothing.
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _pollTimer.Tick += (s, e) => Reassert();
        _pollTimer.Start();
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (handleMatches(hwnd)) return;
        Reassert();

        // Local: skip re-raising when the event came from the widget itself.
        bool handleMatches(IntPtr foreground)
            => new WindowInteropHelper(_window).Handle == foreground;
    }

    /// <summary>
    /// Pushes the widget back to the top of the topmost band without moving,
    /// resizing, showing, or stealing focus. No-op when the widget is hidden.
    /// </summary>
    private void Reassert()
    {
        // Only re-raise a widget the user actually has on screen. If they hid it
        // from the tray we must not force it back into view.
        if (!_window.IsVisible) return;

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero) return;

        SetWindowPos(handle, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollTimer.Stop();

        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
