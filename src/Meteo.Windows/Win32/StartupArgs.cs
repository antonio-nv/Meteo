using System.Globalization;

namespace Meteo.Windows;

public enum StartMode
{
    /// <summary>Meteo.exe, /f[:N] – samostatné okno přes jeden monitor (N = pořadí zleva, od 1). Ukončí se klávesou Esc.</summary>
    Fullscreen,
    /// <summary>/d[:volby] – živé pozadí: okno pod ostatními okny na každém monitoru. Ukončí se klávesou Win+D.</summary>
    Desktop,
    /// <summary>/w [ŠxV] – ladicí režim v běžném okně (nepřekrývá obrazovku, zavře se Esc).</summary>
    Windowed,
}

/// <summary>Parametry příkazové řádky. Neznámý nebo chybějící parametr znamená samostatné okno (<see cref="StartMode.Fullscreen"/>).</summary>
public sealed record StartupArgs(StartMode Mode, int Width, int Height, int Monitor = 0, string? Variant = null)
{
    public static StartupArgs Parse(string[] args)
    {
        if (args.Length == 0) return new(StartMode.Fullscreen, 0, 0);

        var first = args[0].Trim().ToLowerInvariant();
        string? inline = null;
        var colon = first.IndexOf(':');
        if (colon >= 0) { inline = first[(colon + 1)..]; first = first[..colon]; }
        var second = inline ?? (args.Length > 1 ? args[1] : null);

        switch (first)
        {
            case "/f" or "-f":
            {
                var monitor = int.TryParse(second, NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : 0;
                return new(StartMode.Fullscreen, 0, 0, monitor);
            }

            case "/d" or "-d":
                return new(StartMode.Desktop, 0, 0, 0, string.IsNullOrWhiteSpace(inline) ? null : inline);

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
                return new(StartMode.Windowed, w, h);
            }

            default:
                return new(StartMode.Fullscreen, 0, 0);
        }
    }

    /// <summary>
    /// Volby režimu /d z <see cref="Variant"/>, oddělené čárkou:
    /// způsob umístění okna: bez volby "bottom" (okno pod všemi ostatními, ověřeno na Windows 11 24H2),
    /// "auto" (zkusí zavěšení do plochy), nebo vynucený způsob zavěšení legacy, top, between, workerw, progman;
    /// "sw" = kreslit softwarově (jinak grafickou kartou), "diag" = zapsat do error.log výpis oken plochy.
    /// </summary>
    public (string? Method, bool Software, bool Diagnostics) DesktopOptions()
    {
        string? method = null;
        var software = false;
        var diag = false;
        foreach (var token in (Variant ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("sw", StringComparison.OrdinalIgnoreCase)) software = true;
            else if (token.Equals("diag", StringComparison.OrdinalIgnoreCase)) diag = true;
            else method ??= token.ToLowerInvariant();
        }
        // "auto" = null = WallpaperHost zkouší zavěšení do plochy sám; bez volby se použije ověřené "bottom".
        method = method is null ? "bottom" : method == "auto" ? null : method;
        return (method, software, diag);
    }
}
