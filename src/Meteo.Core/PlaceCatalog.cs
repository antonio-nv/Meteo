using System.Globalization;

namespace Meteo.Core;

/// <summary>
/// Místa k zobrazení. Zdroje: vestavěná databáze letišť (ČR + SK, AT, DE, PL; OurAirports, volné dílo),
/// obce přidané přes vyhledávání v nastavení a souřadnice zadané přímo ("49.58, 15.76").
/// Hledání v letištích funguje bez internetu.
/// </summary>
public sealed class PlaceCatalog
{
    private readonly AppSettings _settings;
    private readonly List<Place> _airports;

    public PlaceCatalog(AppSettings settings)
    {
        _settings = settings;
        _airports = LoadAirports();
    }

    public IReadOnlyList<Place> Airports => _airports;

    public bool IsAirport(Place p) => _airports.Any(a => a.Key == p.Key && a.Code == p.Code);

    /// <summary>Převede text z nastavení na místo: ICAO kód, uložená obec, souřadnice nebo přesný název letiště.</summary>
    public Place? Find(string query)
    {
        var q = query?.Trim() ?? "";
        if (q.Length == 0) return null;
        var key = TextUtil.Fold(q);

        var byCode = _airports.FirstOrDefault(p => p.Code.Length > 0 && p.Code == key);
        if (byCode is not null) return byCode;

        var custom = _settings.CustomPlaces.FirstOrDefault(p =>
            TextUtil.Fold(p.Key) == key || TextUtil.Fold(p.Name) == key);
        if (custom is not null) return custom;

        if (TryParseCoordinates(q, out var coords)) return coords;

        return _airports.FirstOrDefault(p => TextUtil.Fold(p.Name) == key);
    }

    /// <summary>Hledání letišť podle kódu, názvu nebo obce (bez diakritiky, bez ohledu na velikost písmen).</summary>
    public IReadOnlyList<Place> SearchAirports(string text, int max = 25)
    {
        var q = TextUtil.Fold(text);
        if (q.Length < 2) return [];

        return _airports
            .Select(p => (Place: p, Rank: Rank(p, q)))
            .Where(t => t.Rank < int.MaxValue)
            .OrderBy(t => t.Rank)
            .ThenBy(t => t.Place.Title, StringComparer.CurrentCulture)
            .Take(max)
            .Select(t => t.Place)
            .ToList();
    }

    private static int Rank(Place p, string q)
    {
        var code = p.Code;
        var name = TextUtil.Fold(p.Name);
        var loc = TextUtil.Fold(p.Locality);
        if (code == q) return 0;
        if (code.StartsWith(q, StringComparison.Ordinal)) return 1;
        if (loc == q || name == q) return 2;
        if (loc.StartsWith(q, StringComparison.Ordinal) || name.StartsWith(q, StringComparison.Ordinal)) return 3;
        if (loc.Contains(q, StringComparison.Ordinal) || name.Contains(q, StringComparison.Ordinal)) return 4;
        return int.MaxValue;
    }

    /// <summary>Místa zvolená pro střídání. Když žádné není platné, vrátí Prahu (LKPR).</summary>
    public IReadOnlyList<Place> Selected()
    {
        var list = new List<Place>();
        foreach (var text in _settings.Places)
        {
            var p = Find(text);
            if (p is not null && !list.Any(x => x.Key == p.Key && x.Latitude == p.Latitude)) list.Add(p);
        }
        if (list.Count == 0 && Find("LKPR") is { } prague) list.Add(prague);
        return list;
    }

    /// <summary>Rozpozná "50.1, 15.2", "50.1;15.2" nebo "50.1 15.2" (desetinná tečka).</summary>
    public static bool TryParseCoordinates(string text, out Place place)
    {
        place = null!;
        var parts = text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return false;
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return false;
        var name = lat.ToString("0.####", CultureInfo.InvariantCulture) + "," + lon.ToString("0.####", CultureInfo.InvariantCulture);
        place = new Place("", name, lat, lon);
        return true;
    }

    private static List<Place> LoadAirports()
    {
        var list = new List<Place>();
        using var stream = typeof(PlaceCatalog).Assembly.GetManifestResourceStream("airports.csv")
                           ?? throw new InvalidOperationException("Chybí vestavěný soubor airports.csv.");
        using var reader = new StreamReader(stream);
        _ = reader.ReadLine(); // hlavička: Code;Name;Locality;Latitude;Longitude
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var p = line.Split(';');
            if (p.Length < 5) continue;
            list.Add(new Place(
                p[0].Trim(), p[1].Trim(),
                double.Parse(p[3], CultureInfo.InvariantCulture),
                double.Parse(p[4], CultureInfo.InvariantCulture),
                p[2].Trim()));
        }
        return list;
    }
}
