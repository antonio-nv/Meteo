using System.Windows.Media;

namespace Meteo.Wpf;

/// <summary>
/// Výběr neproporcionálního písma: 1) nainstalovaný PxPlus IBM VGA, 2) soubor .ttf ve složce "fonts"
/// (složka "fonts" v datovém adresáři Meteo nebo vedle programu), 3) záložní Consolas / Lucida Console / Courier New.
/// Záložní písma jsou přidána jako složená rodina, takže chybějící znaky (např. české) se dokreslí z nich.
/// </summary>
internal static class FontResolver
{
    private const string Fallback = "Consolas, Lucida Console, Courier New";

    private static readonly string[] KnownNames =
    [
        "PxPlus IBM VGA8", "PxPlus IBM VGA 8x16", "Px437 IBM VGA 8x16", "Px437 IBM VGA8",
    ];

    public static FontFamily Resolve(Meteo.Core.AppSettings settings, string dataDir)
    {
        try
        {
            var installed = new HashSet<string>(
                Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);

            foreach (var name in new[] { settings.FontFamily }.Concat(KnownNames))
                if (!string.IsNullOrWhiteSpace(name) && installed.Contains(name))
                    return new FontFamily($"{name}, {Fallback}");

            foreach (var dir in new[] { Path.Combine(dataDir, "fonts"), Path.Combine(AppContext.BaseDirectory, "fonts") })
            {
                if (!Directory.Exists(dir)) continue;
                var families = Fonts.GetFontFamilies(dir);
                var vga = families.FirstOrDefault(f => f.Source.Contains("VGA", StringComparison.OrdinalIgnoreCase));
                if (vga is null) continue;
                var name = vga.FamilyNames.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(name)) continue;
                var baseUri = new Uri(Path.GetFullPath(dir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar);
                return new FontFamily(baseUri, $"./#{name}, {Fallback}");
            }
        }
        catch
        {
            // Cokoli se pokazí při hledání písma → použij záložní.
        }
        return new FontFamily(Fallback);
    }
}
