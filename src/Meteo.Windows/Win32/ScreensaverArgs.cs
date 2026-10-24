using System.Globalization;

namespace Meteo.Windows;

public enum ScreensaverMode
{
    /// <summary>/s – spuštění šetřiče: okna na všech monitorech, zavře se vstupem.</summary>
    Run,
    /// <summary>/c – dialog s nastavením. Totéž dělá .scr bez parametru.</summary>
    Configure,
    /// <summary>/p HWND – náhled v malém okně v dialogu Windows (zde se jen tiše ukončí).</summary>
    Preview,
    /// <summary>/w [ŠxV] – ladicí režim v běžném okně (nepřekrývá obrazovku, zavře se Esc).</summary>
    Windowed,
    /// <summary>/f [N] nebo .exe bez parametru – samostatné okno přes jeden monitor (N = pořadí zleva, od 1).</summary>
    Fullscreen,
}

/// <summary>Parametry příkazové řádky podle konvencí Windows pro soubory .scr, plus režimy pro běžný .exe.</summary>
public sealed record ScreensaverArgs(ScreensaverMode Mode, IntPtr ParentWindow, int Width, int Height, int Monitor = 0)
{
    /// <param name="launchedAsScr">
    /// true, pokud byl program spuštěn jako soubor .scr. Bez parametru to podle konvence znamená nastavení;
    /// běžný .exe bez parametru otevře celoobrazovkové okno.
    /// </param>
    public static ScreensaverArgs Parse(string[] args, bool launchedAsScr = false)
    {
        if (args.Length == 0)
            return new(launchedAsScr ? ScreensaverMode.Configure : ScreensaverMode.Fullscreen, IntPtr.Zero, 0, 0);

        var first = args[0].Trim().ToLowerInvariant();
        string? inline = null;
        var colon = first.IndexOf(':');
        if (colon >= 0) { inline = first[(colon + 1)..]; first = first[..colon]; }
        var second = inline ?? (args.Length > 1 ? args[1] : null);

        switch (first)
        {
            case "/s" or "-s":
                return new(ScreensaverMode.Run, IntPtr.Zero, 0, 0);

            case "/f" or "-f":
            {
                var monitor = int.TryParse(second, NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : 0;
                return new(ScreensaverMode.Fullscreen, IntPtr.Zero, 0, 0, monitor);
            }

            case "/p" or "-p" or "/l" or "-l":
                return new(ScreensaverMode.Preview, ParseHandle(second), 0, 0);

            case "/w" or "-w":
            {
                int w = 1366, h = 768;
                if (second is not null)
                {
                    var parts = second.ToLowerInvariant().Split('x');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pw) &&
                        int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ph) &&
                        pw >= 320 && ph >= 200)
                    {
                        w = pw; h = ph;
                    }
                }
                return new(ScreensaverMode.Windowed, IntPtr.Zero, w, h);
            }

            default:
                // /c, /c:12345 i cokoli neznámého → nastavení
                return new(ScreensaverMode.Configure, ParseHandle(second), 0, 0);
        }
    }

    private static IntPtr ParseHandle(string? s) =>
        long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? new IntPtr(v) : IntPtr.Zero;
}
