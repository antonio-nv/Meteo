using System.Runtime.InteropServices;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>Výčet monitorů ve skutečných pixelech (aplikace musí být Per-Monitor DPI aware – viz app.manifest).</summary>
public sealed class WindowsScreenProvider : IScreenProvider
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var list = new List<ScreenInfo>();

        NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr _, ref NativeMethods.Rect _, IntPtr _) =>
        {
            var info = new NativeMethods.MonitorInfoEx { cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                var r = info.rcMonitor;
                list.Add(new ScreenInfo(
                    info.szDevice ?? "",
                    r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                    (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        if (list.Count == 0) list.Add(new ScreenInfo("DISPLAY", 0, 0, 1366, 768, true));
        // Primární monitor první.
        return list.OrderByDescending(s => s.IsPrimary).ToList();
    }
}
