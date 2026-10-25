using System.Net.Http;

namespace Meteo.Core;

/// <summary>
/// Stažení hotového PNG meteogramu Flymet (Meteopress), např. https://flymet.meteopress.cz/meteogram1/PRIBYSLAV.png.
/// Název souboru se odvodí z obce místa (velká písmena bez diakritiky a mezer); jinak ho lze přepsat v nastavení
/// (AppSettings.FlymetSlugs, klíč = ICAO kód nebo název místa). Neexistující obrázky se na 24 hodin zapamatují.
/// </summary>
public sealed class FlymetClient
{
    private const string BaseUrl = "https://flymet.meteopress.cz/meteogram1/";
    private static readonly TimeSpan MissingTtl = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly string _dir;

    public FlymetClient(HttpClient http, string dataDirectory)
    {
        _http = http;
        _dir = Path.Combine(dataDirectory, "flymet");
        Directory.CreateDirectory(_dir);
    }

    public static string DeriveSlug(string name) => TextUtil.Compact(name);

    public static string SlugFor(Place place, IReadOnlyDictionary<string, string> overrides) =>
        overrides.TryGetValue(place.Key, out var o) && !string.IsNullOrWhiteSpace(o)
            ? o.Trim()
            : DeriveSlug(place.FlymetName);

    public async Task<byte[]?> GetAsync(Place place, IReadOnlyDictionary<string, string> overrides, TimeSpan maxAge, CancellationToken ct)
    {
        var slug = SlugFor(place, overrides);
        if (slug.Length == 0) return null;

        var png = Path.Combine(_dir, slug + ".png");
        var missing = Path.Combine(_dir, slug + ".missing");

        if (File.Exists(png) && DateTime.UtcNow - File.GetLastWriteTimeUtc(png) < maxAge)
            return await File.ReadAllBytesAsync(png, ct).ConfigureAwait(false);

        if (File.Exists(missing) && DateTime.UtcNow - File.GetLastWriteTimeUtc(missing) < MissingTtl)
            return null;

        try
        {
            using var response = await _http.GetAsync(BaseUrl + Uri.EscapeDataString(slug) + ".png", ct).ConfigureAwait(false);
            var type = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!response.IsSuccessStatusCode || !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                try { File.WriteAllText(missing, DateTime.UtcNow.ToString("O")); } catch (IOException) { }
                return null;
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            try { await File.WriteAllBytesAsync(png, bytes, ct).ConfigureAwait(false); } catch (IOException) { }
            return bytes;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Síť nedostupná: použij starší obrázek, pokud existuje.
            return File.Exists(png) ? await File.ReadAllBytesAsync(png, ct).ConfigureAwait(false) : null;
        }
    }
}
