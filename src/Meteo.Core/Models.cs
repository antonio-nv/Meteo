namespace Meteo.Core;

/// <summary>
/// Místo, pro které se zobrazuje předpověď: letiště (Code = ICAO, např. LKPR) nebo obec / libovolný bod (Code je prázdný).
/// Locality je obec, ke které místo patří (z ní se odvozuje název obrázku Flymet).
/// </summary>
public sealed record Place(string Code, string Name, double Latitude, double Longitude, string Locality = "")
{
    /// <summary>Klíč uložený v nastavení: ICAO kód, jinak název.</summary>
    public string Key => Code.Length > 0 ? Code : Name;

    /// <summary>Popisek: "LKPR Václav Havel Airport Prague" nebo "Přibyslav".</summary>
    public string Title => Code.Length > 0 ? Code + " " + Name : Name;

    public string FlymetName => Locality.Length > 0 ? Locality : Name;
}

/// <summary>Hodinové řady předpovědi. Chybějící hodnoty jsou NaN. Čas je lokální (Europe/Prague).</summary>
public sealed class HourlySeries
{
    public DateTime[] Time { get; init; } = [];
    public double[] Temperature { get; init; } = [];
    public double[] ApparentTemperature { get; init; } = [];
    public double[] Humidity { get; init; } = [];
    public double[] Precipitation { get; init; } = [];
    public double[] Snowfall { get; init; } = [];
    public double[] CloudCover { get; init; } = [];
    public double[] Pressure { get; init; } = [];
    /// <summary>Rychlost větru ve 10 m, m/s.</summary>
    public double[] WindSpeed { get; init; } = [];
    /// <summary>Směr, ODKUD vítr vane, stupně (0 = sever).</summary>
    public double[] WindDirection { get; init; } = [];
    public double[] WindGusts { get; init; } = [];
    public bool[] IsDay { get; init; } = [];

    public int Count => Time.Length;
}

public sealed class DailySeries
{
    public DateTime[] Date { get; init; } = [];
    public double[] TempMax { get; init; } = [];
    public double[] TempMin { get; init; } = [];
    public double[] PrecipSum { get; init; } = [];
    public double[] WindMax { get; init; } = [];
    public double[] GustMax { get; init; } = [];
    public DateTime?[] Sunrise { get; init; } = [];
    public DateTime?[] Sunset { get; init; } = [];

    public int Count => Date.Length;
}

public sealed class ForecastResult
{
    public required Place Place { get; init; }
    public required HourlySeries Hourly { get; init; }
    public required DailySeries Daily { get; init; }
    /// <summary>Kdy byla data stažena (UTC).</summary>
    public required DateTime FetchedUtc { get; init; }
    public string Model { get; init; } = "";
}
