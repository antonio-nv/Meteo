namespace Meteo.Core;

/// <summary>Stažení předpovědi s diskovou cache; při výpadku sítě použije poslední uloženou verzi.</summary>
public sealed class ForecastService
{
    private readonly OpenMeteoClient _client;
    private readonly string _cacheDir;

    public ForecastService(OpenMeteoClient client, string dataDirectory)
    {
        _client = client;
        _cacheDir = Path.Combine(dataDirectory, "cache");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<(ForecastResult Result, bool Stale)> GetAsync(Place place, TimeSpan maxAge, CancellationToken ct)
    {
        var path = Path.Combine(_cacheDir, "forecast_" + Slug(place) + ".json");
        string? cached = null;
        var cachedUtc = DateTime.MinValue;

        if (File.Exists(path))
        {
            try
            {
                cachedUtc = File.GetLastWriteTimeUtc(path);
                cached = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            }
            catch (IOException) { cached = null; }
        }

        if (cached != null && DateTime.UtcNow - cachedUtc < maxAge)
        {
            try { return (OpenMeteoClient.Parse(cached, place, cachedUtc), false); }
            catch { /* poškozená cache → stáhni znovu */ }
        }

        try
        {
            var json = await _client.DownloadJsonAsync(place, ct).ConfigureAwait(false);
            var result = OpenMeteoClient.Parse(json, place, DateTime.UtcNow);
            try { await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false); } catch (IOException) { }
            return (result, false);
        }
        catch (Exception) when (cached != null && !ct.IsCancellationRequested)
        {
            return (OpenMeteoClient.Parse(cached, place, cachedUtc), true);
        }
    }

    public static string Slug(Place place) =>
        // Zeměpisná poloha odliší místa stejného jména.
        TextUtil.Compact(place.Key).ToLowerInvariant() + FormattableString.Invariant($"_{place.Latitude:0.00}_{place.Longitude:0.00}");
}
