using System.Globalization;
using System.Net.Http;

namespace Meteo.Core;

/// <summary>Jeden snímek animace. Čas je UTC (u extrapolace čas, pro který platí).</summary>
public sealed record RadarFrame(DateTime TimeUtc, bool IsForecast, int LeadMinutes, string FilePath);

/// <summary>
/// Radarové snímky ČHMÚ z otevřených dat (https://opendata.chmi.cz/meteorology/weather/radar/):
/// maximální odrazivost (MAX_Z; publikuje se po 5 minutách, do animace se bere po 10) a extrapolace na +10 až +60 minut. Snímky se ukládají do cache,
/// takže se pokaždé stahují jen nové. Žádná z operací nehází výjimku kvůli síti – při výpadku vrátí to, co je v cache.
/// </summary>
public sealed class RadarClient
{
    public const string BaseUrl = "https://opendata.chmi.cz/meteorology/weather/radar/composite/";
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PastStep = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GapMemory = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly string _dir;

    public RadarClient(HttpClient http, string dataDirectory)
    {
        _http = http;
        _dir = Path.Combine(dataDirectory, "radar");
        Directory.CreateDirectory(_dir);
    }

    public static DateTime Floor5(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % Step.Ticks, DateTimeKind.Utc);

    /// <summary>Název souboru pozorovaného snímku (čas UTC).</summary>
    public static string FileName(DateTime t) =>
        "pacz2gmaps3.z_max3d." + t.ToString("yyyyMMdd.HHmm", CultureInfo.InvariantCulture) + ".0.png";

    /// <summary>Čas (UTC) z názvu pozorovaného snímku, jinak null.</summary>
    public static DateTime? ParseFileName(string name)
    {
        const string prefix = "pacz2gmaps3.z_max3d.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".0.png", StringComparison.Ordinal)) return null;
        var stamp = name.Substring(prefix.Length, name.Length - prefix.Length - ".0.png".Length);
        return DateTime.TryParseExact(stamp, "yyyyMMdd.HHmm", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
            ? DateTime.SpecifyKind(t, DateTimeKind.Utc) : null;
    }

    public static string ForecastName(DateTime calc, int leadMinutes) =>
        "pacz2gmaps3.fct_z_max." + calc.ToString("yyyyMMdd.HHmm", CultureInfo.InvariantCulture) + "." +
        leadMinutes.ToString(CultureInfo.InvariantCulture) + ".png";

    public static string ObservedUrl(DateTime t) => BaseUrl + "maxz/png/" + FileName(t);
    public static string ForecastUrl(DateTime calc, int leadMinutes) => BaseUrl + "fct_maxz/png/" + ForecastName(calc, leadMinutes);

    /// <param name="pastHours">Kolik hodin zpět se přehrává. Bere se každý druhý snímek (po 10 minutách):
    /// pro 2 hodiny je to 13 snímků včetně nejnovějšího a nejstaršího.</param>
    /// <param name="includeForecast">Přidat extrapolaci ČHMÚ na +10 až +60 minut.</param>
    public async Task<IReadOnlyList<RadarFrame>> GetFramesAsync(
        int pastHours, DateTime utcNow, CancellationToken ct, bool includeForecast = false)
    {
        var pastFrames = Math.Clamp(pastHours, 1, 6) * 6 + 1;
        var frames = new List<RadarFrame>();
        var offline = false;

        async Task<string?> Ensure(string url, string name, bool recent)
        {
            var path = Path.Combine(_dir, name);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
            if (offline) return null;
            var marker = path + ".missing";
            if (File.Exists(marker)) return null;
            try
            {
                using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P')
                    {
                        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
                        return path;
                    }
                }
                // Čerstvý snímek se ještě nemusel zveřejnit; trvalou mezeru si pamatujeme jen u starších.
                if (!recent) { try { File.WriteAllText(marker, ""); } catch (IOException) { } }
                return null;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                offline = true; // nezdržuj se dalšími pokusy
                return null;
            }
        }

        // 1) nejnovější pozorovaný snímek: zkoušíme od aktuálního času o 5 minut dozadu
        var start = Floor5(utcNow);
        DateTime? latest = null;
        for (var k = 0; k < 10 && latest is null; k++)
        {
            var t = start - k * Step;
            if (await Ensure(ObservedUrl(t), FileName(t), recent: true).ConfigureAwait(false) is { } p)
            {
                latest = t;
                frames.Add(new RadarFrame(t, false, 0, p));
            }
            if (offline) break;
        }

        if (latest is null)
            return FromCache(pastFrames);

        // 2) starší snímky
        for (var i = 1; i < pastFrames; i++)
        {
            var t = latest.Value - i * PastStep;
            var recent = utcNow - t < GapMemory;
            if (await Ensure(ObservedUrl(t), FileName(t), recent).ConfigureAwait(false) is { } p)
                frames.Add(new RadarFrame(t, false, 0, p));
        }
        frames.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));

        // 3) extrapolace: čas výpočtu bývá shodný s posledním snímkem, případně o krok dva starší
        if (includeForecast)
        {
            DateTime? calc = null;
            for (var k = 0; k < 3 && calc is null; k++)
            {
                var c = latest.Value - k * Step;
                if (await Ensure(ForecastUrl(c, 10), ForecastName(c, 10), recent: true).ConfigureAwait(false) is { } p)
                {
                    calc = c;
                    frames.Add(new RadarFrame(c + TimeSpan.FromMinutes(10), true, 10, p));
                }
                if (offline) break;
            }
            if (calc is { } cc)
                for (var lead = 20; lead <= 60; lead += 10)
                    if (await Ensure(ForecastUrl(cc, lead), ForecastName(cc, lead), recent: true).ConfigureAwait(false) is { } p)
                        frames.Add(new RadarFrame(cc + TimeSpan.FromMinutes(lead), true, lead, p));
        }

        Cleanup(utcNow);
        return frames;
    }

    /// <summary>Bez sítě: posledních několik uložených pozorovaných snímků.</summary>
    private List<RadarFrame> FromCache(int pastFrames)
    {
        var list = new List<RadarFrame>();
        foreach (var path in Directory.EnumerateFiles(_dir, "pacz2gmaps3.z_max3d.*.0.png"))
            if (ParseFileName(Path.GetFileName(path)) is { } t)
                list.Add(new RadarFrame(t, false, 0, path));
        return list.OrderBy(f => f.TimeUtc).TakeLast(pastFrames).ToList();
    }

    private void Cleanup(DateTime utcNow)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(_dir))
                if (utcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(3))
                    File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
