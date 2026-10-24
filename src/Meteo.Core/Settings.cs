using System.Text.Json;

namespace Meteo.Core;

public sealed class AppSettings
{
    /// <summary>
    /// Místa, která se v šetřiči střídají, v tomto pořadí. Každé místo je ICAO kód letiště (LKPR), název dříve přidané obce
    /// nebo souřadnice ("49.58, 15.76").
    /// </summary>
    public List<string> Places { get; set; } = ["LKLT", "LKPR", "LKKO", "LKSU", "LKTB", "LKJI", "LKPI"];

    /// <summary>Obce přidané přes vyhledávání v nastavení (souřadnice se dohledají automaticky a uloží sem).</summary>
    public List<Place> CustomPlaces { get; set; } = [];

    /// <summary>Jak dlouho se zobrazuje jedno město.</summary>
    public int RotateSeconds { get; set; } = 60;

    /// <summary>Po jak dlouhé době se předpověď stahuje znovu (model se aktualizuje po 6 hodinách).</summary>
    public int RefreshMinutes { get; set; } = 60;

    /// <summary>Celočíselné zvětšení písma 8x16 (0 = automaticky podle rozlišení).</summary>
    public int UiScale { get; set; } = 0;

    /// <summary>Preferovaný neproporcionální font. Když není nainstalovaný, použije se záložní.</summary>
    public string FontFamily { get; set; } = "PxPlus IBM VGA8";

    /// <summary>Zobrazit i meteogram Flymet (Meteopress), pokud pro dané město existuje.</summary>
    public bool ShowFlymet { get; set; } = true;

    /// <summary>Zobrazit animaci srážkového radaru ČHMÚ.</summary>
    public bool ShowRadar { get; set; } = true;

    /// <summary>Kolik hodin zpět se v animaci radaru přehrává (snímky po 10 minutách).</summary>
    public int RadarHours { get; set; } = 2;

    /// <summary>K radaru přidat extrapolaci ČHMÚ na dalších 60 minut.</summary>
    public bool RadarForecast { get; set; } = true;

    /// <summary>Ručně zadané názvy souborů Flymet podle klíče místa, např. { "LKPI": "PRIBYSLAV" }.</summary>
    public Dictionary<string, string> FlymetSlugs { get; set; } = new();

    /// <summary>true = stejný obsah na všech monitorech, false = ostatní monitory jsou černé.</summary>
    public bool AllMonitors { get; set; } = true;

    /// <summary>Monitor, na kterém naposledy běželo samostatné okno (název zařízení, např. \\.\DISPLAY2).</summary>
    public string LastMonitor { get; set; } = "";

    /// <summary>Nahradí háčky a čárky ASCII písmeny (pro fonty bez české diakritiky).</summary>
    public bool AsciiOnly { get; set; } = false;
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string DataDirectory { get; }
    public string FilePath => Path.Combine(DataDirectory, "settings.json");

    public SettingsStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Meteo");
        Directory.CreateDirectory(DataDirectory);
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch
        {
            // Poškozený soubor: pokračuj s výchozím nastavením, nepadej v šetřiči.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
