using System.Net.Http;
using System.Text.Json;

namespace Meteo.Core;

/// <summary>Vyhledání obce podle názvu → souřadnice, aby se nemusely zadávat ručně (Open-Meteo Geocoding).</summary>
public sealed class GeocodingClient
{
    private readonly HttpClient _http;

    public GeocodingClient(HttpClient http) => _http = http;

    public sealed record Hit(Place Place, string Display);

    /// <summary>Nejdřív hledá v ČR; když je výsledků málo, doplní je i z okolních zemí.</summary>
    public async Task<IReadOnlyList<Hit>> SearchAsync(string name, CancellationToken ct, int count = 20)
    {
        var list = new List<Hit>(await QueryAsync(name, "CZ", count, ct).ConfigureAwait(false));
        if (list.Count < 5)
        {
            foreach (var cc in new[] { "SK", "AT", "DE", "PL" })
            {
                foreach (var h in await QueryAsync(name, cc, 5, ct).ConfigureAwait(false))
                    if (!list.Any(x => Math.Abs(x.Place.Latitude - h.Place.Latitude) < 0.001 &&
                                       Math.Abs(x.Place.Longitude - h.Place.Longitude) < 0.001))
                        list.Add(h);
            }
        }
        return list;
    }

    private async Task<List<Hit>> QueryAsync(string name, string countryCode, int count, CancellationToken ct)
    {
        var url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name) +
                  $"&count={count}&language=cs&countryCode={countryCode}&format=json";
        var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(json);
        var list = new List<Hit>();
        if (!doc.RootElement.TryGetProperty("results", out var results)) return list;

        foreach (var r in results.EnumerateArray())
        {
            var n = r.GetProperty("name").GetString() ?? "";
            var lat = r.GetProperty("latitude").GetDouble();
            var lon = r.GetProperty("longitude").GetDouble();
            var region = r.TryGetProperty("admin1", out var a) ? a.GetString() : null;
            var elevation = r.TryGetProperty("elevation", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN;

            var display = n;
            if (countryCode != "CZ") display += " (" + countryCode + ")";
            if (!string.IsNullOrEmpty(region)) display += ", " + region;
            if (!double.IsNaN(elevation)) display += $" ({elevation:0} m n. m.)";
            list.Add(new Hit(new Place("", n, lat, lon, n), display));
        }
        return list;
    }
}
