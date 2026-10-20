using System.Runtime.InteropServices;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>
/// Hlídá, aby okno zůstalo přesně na svém monitoru: zahodí jakoukoli změnu polohy a velikosti
/// (Win+šipky, Aero Snap, tažení, DPI přepočty) a zablokuje systémové příkazy velikost / přesun / maximalizace.
/// Je to čistý Win32: WPF ji zapojí přes HwndSource.AddHook(guard.WndProc).
/// Minimalizace (např. kliknutím na tlačítko na hlavním panelu) zůstává povolená, aby šlo okno schovat.
/// </summary>
public sealed class FullscreenGuard
{
    /// <summary>Monitor, na kterém má okno být. Při přestěhování se nastaví PŘED voláním SetWindowPos.</summary>
    public ScreenInfo? Target { get; set; }

    /// <summary>Zjištění, zda je okno minimalizované (vyměnitelné kvůli testům mimo Windows).</summary>
    public Func<IntPtr, bool> IsMinimized { get; set; } = NativeMethods.IsIconic;

    public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_SYSCOMMAND:
            {
                var cmd = (int)(wParam.ToInt64() & 0xFFF0);
                if (cmd is NativeMethods.SC_SIZE or NativeMethods.SC_MOVE or NativeMethods.SC_MAXIMIZE or NativeMethods.SC_KEYMENU)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                break;
            }

            case NativeMethods.WM_WINDOWPOSCHANGING:
            {
                var t = Target;
                if (t is null || IsMinimized(hwnd)) break;

                var wp = Marshal.PtrToStructure<NativeMethods.WindowPos>(lParam);
                // Při minimalizaci Windows posílá polohu daleko mimo obrazovku – tu neopravujeme.
                if (wp.x <= -30000 || wp.y <= -30000) break;

                var changed = false;
                if ((wp.flags & NativeMethods.SWP_NOMOVE) == 0 && (wp.x != t.X || wp.y != t.Y))
                {
                    wp.x = t.X;
                    wp.y = t.Y;
                    changed = true;
                }
                if ((wp.flags & NativeMethods.SWP_NOSIZE) == 0 && (wp.cx != t.Width || wp.cy != t.Height))
                {
                    wp.cx = t.Width;
                    wp.cy = t.Height;
                    changed = true;
                }
                if (changed) Marshal.StructureToPtr(wp, lParam, false);
                break;
            }
        }
        return IntPtr.Zero;
    }
}
