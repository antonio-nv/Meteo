using System.Globalization;

namespace Meteo.Core;

public readonly record struct GeoPoint(double Lat, double Lon);

public sealed record Landmark(string Name, double Lat, double Lon);

/// <summary>
/// Georeference snímků radaru ČHMÚ (PNG, EPSG:3857 Web Mercator, 1 × 1 km):
/// celý obrázek pokrývá E 11.267°–20.770°, N 48.047°–52.167° (radar_description_en.pdf, opendata.chmi.cz).
/// </summary>
public static class RadarGeo
{
    public const double LonMin = 11.267, LonMax = 20.770, LatMin = 48.047, LatMax = 52.167;

    public static double MercatorY(double latDegrees) =>
        Math.Log(Math.Tan(Math.PI / 4 + latDegrees * Math.PI / 360.0));

    /// <summary>Poměr šířky k výšce obrázku (≈ 1,478; při 1 km/px ≈ 1058 × 716 px).</summary>
    public static double Aspect =>
        ((LonMax - LonMin) * Math.PI / 180.0) / (MercatorY(LatMax) - MercatorY(LatMin));

    /// <summary>Poloha v obrázku jako zlomek 0..1 (x zleva doprava, y shora dolů).</summary>
    public static (double X, double Y) ToUnit(double lat, double lon) =>
        ((lon - LonMin) / (LonMax - LonMin),
         (MercatorY(LatMax) - MercatorY(lat)) / (MercatorY(LatMax) - MercatorY(LatMin)));

    public static bool IsInside(double lat, double lon) =>
        lat >= LatMin && lat <= LatMax && lon >= LonMin && lon <= LonMax;

    /// <summary>Velká města pro orientaci na mapě radaru.</summary>
    public static readonly Landmark[] Cities =
    [
        new("Praha", 50.0755, 14.4378), new("Brno", 49.1951, 16.6068), new("Ostrava", 49.8209, 18.2625),
        new("Plzeň", 49.7384, 13.3736), new("Liberec", 50.7663, 15.0543), new("Olomouc", 49.5938, 17.2509),
        new("Č. Budějovice", 48.9745, 14.4743), new("Hradec Král.", 50.2104, 15.8252), new("Ústí n. L.", 50.6607, 14.0323),
        new("Pardubice", 50.0343, 15.7812), new("Zlín", 49.2265, 17.6627), new("K. Vary", 50.2319, 12.8720),
        new("Jihlava", 49.3961, 15.5912),
        new("Wien", 48.2082, 16.3738), new("Dresden", 51.0504, 13.7373), new("Wrocław", 51.1079, 17.0385),
        new("Bratislava", 48.1486, 17.1077), new("Katowice", 50.2649, 19.0238), new("Kraków", 50.0647, 19.9450),
    ];
}

/// <summary>Státní hranice (B) a hranice krajů ČR (R) z Natural Earth (volné dílo), zjednodušené, pro podklad radaru.</summary>
public sealed class MapData
{
    public IReadOnlyList<GeoPoint[]> Borders { get; }
    public IReadOnlyList<GeoPoint[]> Regions { get; }

    private MapData(List<GeoPoint[]> borders, List<GeoPoint[]> regions)
    {
        Borders = borders;
        Regions = regions;
    }

    public static MapData Load()
    {
        using var stream = typeof(MapData).Assembly.GetManifestResourceStream("map.txt")
                           ?? throw new InvalidOperationException("Chybí vestavěný soubor map.txt.");
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    public static MapData Parse(TextReader reader)
    {
        var borders = new List<GeoPoint[]>();
        var regions = new List<GeoPoint[]>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length < 3 || line[0] == '#') continue;
            var target = line[0] switch { 'B' => borders, 'R' => regions, _ => null };
            if (target is null) continue;

            var pts = new List<GeoPoint>();
            foreach (var token in line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var c = token.Split(',');
                if (c.Length != 2) continue;
                pts.Add(new GeoPoint(
                    double.Parse(c[1], CultureInfo.InvariantCulture),
                    double.Parse(c[0], CultureInfo.InvariantCulture)));
            }
            if (pts.Count >= 2) target.Add(pts.ToArray());
        }
        return new MapData(borders, regions);
    }
}
