namespace Meteo.Core;

/// <summary>
/// Nastavení programu. Nikde se neukládá do souboru: mění se přímo zde ve zdrojovém kódu a po úpravě stačí program znovu sestavit.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Místa, která se střídají, v tomto pořadí. Každé místo je ICAO kód letiště (LKPR), název místa z <see cref="CustomPlaces"/>
    /// nebo souřadnice ("49.58, 15.76").
    /// </summary>
    public List<string> Places { get; set; } = ["LKLT", "LKPR", "LKKO", "LKSU", "LKTB", "LKJI", "LKPI", "Nové Veselí"];

    /// <summary>Vlastní místa mimo databázi letišť: Place(ICAO nebo "", název, zeměpisná šířka, délka, obec pro Flymet).</summary>
    public List<Place> CustomPlaces { get; set; } = [new Place("", "Nové Veselí", 49.5197, 15.9086, "Nové Veselí")];

    /// <summary>Jak dlouho se zobrazuje jedno místo (nejméně 10 s).</summary>
    public int RotateSeconds { get; set; } = 10;

    /// <summary>Po jak dlouhé době se předpověď stahuje znovu (model se aktualizuje po 6 hodinách).</summary>
    public int RefreshMinutes { get; set; } = 60;

    /// <summary>Celočíselné zvětšení písma 8x16 (0 = automaticky podle rozlišení).</summary>
    public int UiScale { get; set; } = 0;

    /// <summary>Preferovaný neproporcionální font. Když není nainstalovaný, použije se záložní.</summary>
    public string FontFamily { get; set; } = "PxPlus IBM VGA8";

    /// <summary>Zobrazit i meteogram Flymet (Meteopress), pokud pro dané místo existuje.</summary>
    public bool ShowFlymet { get; set; } = true;

    /// <summary>Zobrazit animaci srážkového radaru ČHMÚ.</summary>
    public bool ShowRadar { get; set; } = true;

    /// <summary>Kolik hodin zpět se v animaci radaru přehrává (snímky po 10 minutách).</summary>
    public int RadarHours { get; set; } = 2;

    /// <summary>K radaru přidat extrapolaci ČHMÚ na dalších 60 minut.</summary>
    public bool RadarForecast { get; set; } = true;

    /// <summary>
    /// Názvy obrázků Flymet podle klíče místa (ICAO kód nebo název), pokud se liší od názvu obce místa.
    /// LKSU leží v Novém Malíně, meteogram je pro Šumperk. Obrázek je https://flymet.meteopress.cz/meteogram1/NÁZEV.png.
    /// </summary>
    public Dictionary<string, string> FlymetSlugs { get; set; } = new() { ["LKSU"] = "SUMPERK" };

    /// <summary>true = stejný obsah na všech monitorech (živé pozadí), false = ostatní monitory jsou černé.</summary>
    public bool AllMonitors { get; set; } = true;

    /// <summary>Monitor pro samostatné okno (pořadí zleva, od 1). 0 = hlavní monitor. Přebije ho parametr /f:N.</summary>
    public int Monitor { get; set; } = 0;

    /// <summary>Nahradí háčky a čárky ASCII písmeny (pro fonty bez české diakritiky).</summary>
    public bool AsciiOnly { get; set; } = false;
}
