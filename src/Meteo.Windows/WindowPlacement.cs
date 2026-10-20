using Meteo.Core;

namespace Meteo.Windows;

public static class WindowPlacement
{
    /// <summary>
    /// Přesně na pixely nastaví okno přes celý monitor (WPF pracuje v DIP, tady v pixelech).
    /// topmost = true: nad všemi okny (šetřič). topmost = false: pořadí oken se nemění, okno může být překryto jiným.
    /// </summary>
    public static void CoverScreen(IntPtr hwnd, ScreenInfo screen, bool topmost = true)
    {
        if (hwnd == IntPtr.Zero) return;
        var flags = NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW;
        if (!topmost) flags |= NativeMethods.SWP_NOZORDER;
        NativeMethods.SetWindowPos(
            hwnd,
            topmost ? NativeMethods.HWND_TOPMOST : IntPtr.Zero,
            screen.X, screen.Y, screen.Width, screen.Height,
            flags);
    }
}
