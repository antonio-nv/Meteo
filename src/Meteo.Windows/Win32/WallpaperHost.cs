using Meteo.Core;

namespace Meteo.Windows;

/// <summary>Jak se podařilo okno zavěsit do plochy.</summary>
public enum AttachMethod
{
    /// <summary>Nezavěšeno: okno je jen obyčejné okno pod všemi ostatními (nouzový režim).</summary>
    None,
    /// <summary>Starší Windows (do 23H2): sourozenec WorkerW za oknem s ikonami plochy.</summary>
    LegacyWorkerW,
    /// <summary>Windows 11 24H2 a novější: WorkerW jako potomek okna Progman.</summary>
    ProgmanChildWorkerW,
    /// <summary>Windows 11 24H2: potomek okna Progman, v pořadí oken těsně pod ikonami plochy a nad tapetou (WorkerW).</summary>
    ProgmanBetween,
    /// <summary>Windows 11 24H2: potomek okna Progman úplně nahoře, nad ikonami plochy (ty jsou pod ním skryté).</summary>
    ProgmanTop,
    /// <summary>Záloha: přímo potomek okna Progman, na dně pořadí oken.</summary>
    ProgmanDirect,
}

public sealed record AttachResult(AttachMethod Method, bool Embedded, string Detail);

/// <summary>Okna plochy, kterými se hledá rodič pro živé pozadí. Rozhraní kvůli testům s falešnou strukturou oken.</summary>
public interface IDesktopWindows
{
    IntPtr FindProgman();
    /// <summary>Pošle Progmanu zprávu 0x052C, aby vytvořil okno WorkerW za ikonami.</summary>
    void SpawnWorkerW(IntPtr progman);
    IEnumerable<IntPtr> TopLevelWindows();
    /// <summary>Přímý potomek dané třídy, jinak <see cref="IntPtr.Zero"/>.</summary>
    IntPtr FindChild(IntPtr parent, string className);
    /// <summary>Další okno nejvyšší úrovně dané třídy za oknem <paramref name="after"/>.</summary>
    IntPtr NextTopLevel(IntPtr after, string className);
}

/// <summary>
/// Vloží okno do plochy Windows: za ikony, před tapetu, mimo hlavní panel a Alt+Tab. Není to oficiální rozhraní Windows,
/// ale dlouhodobě používaný postup (aplikace s živými tapetami); od verze 24H2 se uspořádání oken plochy změnilo,
/// proto se zkouší víc způsobů a výsledek se zapisuje do <see cref="AttachResult"/>.
/// </summary>
public static class WallpaperHost
{
    /// <summary>
    /// Vybere rodiče a případně okno, za které se vloží v pořadí oken (<c>InsertAfter</c>, jinak <see cref="IntPtr.Zero"/>).
    /// <paramref name="variant"/> (z /d:varianta) vynutí jeden způsob:
    /// "legacy", "top", "between", "workerw", "progman", "bottom" (nic nevkládat). Jinak se zkouší automaticky v tomto pořadí.
    /// </summary>
    public static (IntPtr Parent, AttachMethod Method, IntPtr InsertAfter) FindParent(IDesktopWindows w, string? variant = null)
    {
        variant = variant?.Trim().ToLowerInvariant();
        if (variant == "bottom") return (IntPtr.Zero, AttachMethod.None, IntPtr.Zero);

        var progman = w.FindProgman();
        if (progman == IntPtr.Zero) return (IntPtr.Zero, AttachMethod.None, IntPtr.Zero);
        w.SpawnWorkerW(progman);

        if (variant is null or "legacy")
        {
            // Dřívější uspořádání: okno s ikonami (SHELLDLL_DefView) leží v samostatném WorkerW a další WorkerW za ním je pozadí.
            // Progman sám se přeskakuje, protože ve Windows 11 24H2 ikony drží on a nesouvisející WorkerW by byl špatný cíl.
            foreach (var top in w.TopLevelWindows())
            {
                if (top == progman || w.FindChild(top, "SHELLDLL_DefView") == IntPtr.Zero) continue;
                var worker = w.NextTopLevel(top, "WorkerW");
                if (worker != IntPtr.Zero) return (worker, AttachMethod.LegacyWorkerW, IntPtr.Zero);
            }
            if (variant == "legacy") return (IntPtr.Zero, AttachMethod.None, IntPtr.Zero);
        }

        var defView = w.FindChild(progman, "SHELLDLL_DefView");
        var child = w.FindChild(progman, "WorkerW");

        // 24H2 (ikony drží Progman): okno pod ikonami se na ověřeném sestavení 10.0.26300 nezobrazilo, protože vrstva ikon
        // zakrývá vše pod sebou. Proto se okno dává nahoru nad ikony (ikony jsou pak skryté, pozadí zůstává vidět i s Win+D).
        if (variant == "top" || (variant is null && defView != IntPtr.Zero))
            return (progman, AttachMethod.ProgmanTop, IntPtr.Zero);

        if (variant is "between")
        {
            // 24H2: Progman má dvě děti, ikony (SHELLDLL_DefView) nahoře a pod nimi WorkerW s tapetou. Naše okno patří mezi ně:
            // potomek Progmanu vložený hned pod ikony, tedy nad tapetu.
            if (defView != IntPtr.Zero && child != IntPtr.Zero) return (progman, AttachMethod.ProgmanBetween, defView);
            if (variant == "between") return (IntPtr.Zero, AttachMethod.None, IntPtr.Zero);
        }

        if (variant is null or "workerw")
        {
            if (child != IntPtr.Zero) return (child, AttachMethod.ProgmanChildWorkerW, IntPtr.Zero);
            if (variant == "workerw") return (IntPtr.Zero, AttachMethod.None, IntPtr.Zero);
        }

        return (progman, AttachMethod.ProgmanDirect, IntPtr.Zero);
    }

    /// <summary>Zavěsí okno <paramref name="hwnd"/> do plochy přesně přes <paramref name="screen"/>.</summary>
    public static AttachResult Attach(IntPtr hwnd, ScreenInfo screen, string? variant = null, IDesktopWindows? api = null)
    {
        if (hwnd == IntPtr.Zero) return new(AttachMethod.None, false, "okno nemá HWND");
        api ??= new Win32DesktopWindows();

        // Z okna nikdy nevznikne položka v Alt+Tab ani na hlavním panelu.
        var ex = DesktopNative.GetWindowLong(hwnd, DesktopNative.GWL_EXSTYLE);
        ex = (ex | DesktopNative.WS_EX_TOOLWINDOW | DesktopNative.WS_EX_NOACTIVATE) & ~DesktopNative.WS_EX_APPWINDOW;
        DesktopNative.SetWindowLong(hwnd, DesktopNative.GWL_EXSTYLE, ex);

        var (parent, method, insertAfter) = FindParent(api, variant);
        if (parent == IntPtr.Zero)
        {
            SendToBottom(hwnd, screen);
            return new(AttachMethod.None, false, variant == "bottom"
                ? "vynuceno: okno pod ostatními"
                : "rodič plochy nenalezen, okno je jen pod ostatními");
        }

        var oldStyle = DesktopNative.GetWindowLong(hwnd, DesktopNative.GWL_STYLE);
        var childStyle = (oldStyle & ~(DesktopNative.WS_POPUP | DesktopNative.WS_CAPTION | DesktopNative.WS_SYSMENU |
                                       DesktopNative.WS_THICKFRAME | DesktopNative.WS_MINIMIZEBOX | DesktopNative.WS_MAXIMIZEBOX))
                         | DesktopNative.WS_CHILD | DesktopNative.WS_VISIBLE;

        // Podle dokumentace SetParent se styl mění před přepnutím rodiče (popup → child).
        DesktopNative.SetWindowLong(hwnd, DesktopNative.GWL_STYLE, childStyle);
        DesktopNative.SetParent(hwnd, parent);

        if (DesktopNative.GetParent(hwnd) != parent)
        {
            DesktopNative.SetWindowLong(hwnd, DesktopNative.GWL_STYLE, oldStyle); // vrátit, ať okno nezůstane napůl
            SendToBottom(hwnd, screen);
            return new(AttachMethod.None, false, $"SetParent selhal ({method}), okno je jen pod ostatními");
        }

        // Souřadnice potomka jsou vůči rodiči: přepočet z virtuální plochy.
        var pt = new DesktopNative.NativePoint { X = screen.X, Y = screen.Y };
        DesktopNative.ScreenToClient(parent, ref pt);
        var flags = NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW;
        var after = IntPtr.Zero; // HWND_TOP
        if (insertAfter != IntPtr.Zero) after = insertAfter;
        else if (method == AttachMethod.ProgmanDirect) after = DesktopNative.HWND_BOTTOM;
        else if (method != AttachMethod.ProgmanTop) flags |= NativeMethods.SWP_NOZORDER; // pořadí se nemění
        NativeMethods.SetWindowPos(hwnd, after, pt.X, pt.Y, screen.Width, screen.Height, flags);

        return new(method, true, $"zavěšeno ({method}) na {screen.Name} {screen.Width}x{screen.Height}");
    }

    private static void SendToBottom(IntPtr hwnd, ScreenInfo screen) =>
        NativeMethods.SetWindowPos(hwnd, DesktopNative.HWND_BOTTOM, screen.X, screen.Y, screen.Width, screen.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

    /// <summary>Po ukončení donutí Průzkumníka znovu vykreslit tapetu (okno živého pozadí už nezakrývá).</summary>
    public static void RefreshWallpaper() =>
        DesktopNative.SystemParametersInfo(DesktopNative.SPI_SETDESKWALLPAPER, 0, null, DesktopNative.SPIF_UPDATEINIFILE);
}

/// <summary>Skutečná okna Windows.</summary>
internal sealed class Win32DesktopWindows : IDesktopWindows
{
    public IntPtr FindProgman() => DesktopNative.FindWindow("Progman", null);

    public void SpawnWorkerW(IntPtr progman) =>
        DesktopNative.SendMessageTimeout(progman, DesktopNative.WM_SPAWN_WORKERW, new UIntPtr(0xD), new IntPtr(1),
            DesktopNative.SMTO_NORMAL, 1000, out _);

    public IEnumerable<IntPtr> TopLevelWindows()
    {
        var list = new List<IntPtr>();
        DesktopNative.EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    public IntPtr FindChild(IntPtr parent, string className) =>
        DesktopNative.FindWindowEx(parent, IntPtr.Zero, className, null);

    public IntPtr NextTopLevel(IntPtr after, string className) =>
        DesktopNative.FindWindowEx(IntPtr.Zero, after, className, null);
}
