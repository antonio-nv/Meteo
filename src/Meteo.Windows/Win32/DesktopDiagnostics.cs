using System.Text;

namespace Meteo.Windows;

/// <summary>Výpis oken plochy do logu, když se živé pozadí nezobrazí tam, kde má (jen pro ladění režimu /d).</summary>
public static class DesktopDiagnostics
{
    private const uint GW_HWNDNEXT = 2, GW_CHILD = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeMethods.Rect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

    public static string Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "(žádné)";
        GetWindowRect(hwnd, out var r);
        var style = DesktopNative.GetWindowLong(hwnd, DesktopNative.GWL_STYLE);
        var ex = DesktopNative.GetWindowLong(hwnd, DesktopNative.GWL_EXSTYLE);
        return $"0x{hwnd.ToInt64():X} {DesktopNative.ClassNameOf(hwnd)} viditelné={IsWindowVisible(hwnd)} " +
               $"obdélník=({r.Left},{r.Top})-({r.Right},{r.Bottom}) styl=0x{style:X8} exstyl=0x{ex:X8}";
    }

    /// <summary>Okno, jeho řetěz rodičů a celý strom Progmanu (děti shora dolů v pořadí oken).</summary>
    public static string Dump(IntPtr ours)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  naše okno: " + Describe(ours));
        for (var p = DesktopNative.GetParent(ours); p != IntPtr.Zero; p = DesktopNative.GetParent(p))
            sb.AppendLine("    rodič: " + Describe(p));
        var progman = DesktopNative.FindWindow("Progman", null);
        sb.AppendLine("  Progman: " + Describe(progman));
        Tree(progman, 2, sb, ours);
        return sb.ToString().TrimEnd();
    }

    private static void Tree(IntPtr parent, int depth, StringBuilder sb, IntPtr ours)
    {
        if (depth > 4) return;
        for (var c = GetWindow(parent, GW_CHILD); c != IntPtr.Zero; c = GetWindow(c, GW_HWNDNEXT))
        {
            sb.AppendLine(new string(' ', depth * 2 + 2) + (c == ours ? "→ " : "- ") + Describe(c));
            Tree(c, depth + 1, sb, ours);
        }
    }
}
