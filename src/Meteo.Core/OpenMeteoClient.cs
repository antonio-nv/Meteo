using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Meteo.Core;

/// <summary>
/// Předpověď numerického modelu ALADIN (ČHMÚ) přes bezplatné API Open-Meteo.
/// Open-Meteo je nekomerční a bez klíče; data jsou pod licencí CC BY 4.0 (je třeba uvést zdroj).
/// </summary>
public sealed class OpenMeteoClient
{
    public const string Model = "chmi_aladin_seamless";

    private const string HourlyVars =
        "temperature_2m,apparent_temperature,relative_humidity_2m,precipitation,snowfall,cloud_cover,cloud_cover_low,cloud_cover_mid,cloud_cover_high," +
        "pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m,is_day";

    private const string DailyVars =
        "temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max,wind_gusts_10m_max,sunrise,sunset";

    private readonly HttpClient _http;

    public OpenMeteoClient(HttpClient http) => _http = http;

    public static string BuildUrl(Place place) =>
        FormattableString.Invariant(
            $"https://api.open-meteo.com/v1/forecast?latitude={place.Latitude:0.####}&longitude={place.Longitude:0.####}") +
        $"&hourly={HourlyVars}&daily={DailyVars}&models={Model}&wind_speed_unit=ms" +
        "&timezone=Europe%2FPrague&forecast_days=3";

    public async Task<string> DownloadJsonAsync(Place place, CancellationToken ct)
    {
        using var response = await _http.GetAsync(BuildUrl(place), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Open-Meteo {(int)response.StatusCode}: {ExtractReason(body)}");
        return body;
    }

    private static string ExtractReason(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("reason", out var r)) return r.GetString() ?? body;
        }
        catch (JsonException) { }
        return body.Length > 200 ? body[..200] : body;
    }

    public static ForecastResult Parse(string json, Place place, DateTime fetchedUtc)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException(root.TryGetProperty("reason", out var r) ? r.GetString() : "Chyba API");

        var h = root.GetProperty("hourly");
        var times = Times(h.GetProperty("time"));
        var n = times.Length;

        var hourly = new HourlySeries
        {
            Time = times,
            Temperature = Series(h, "temperature_2m", n),
            ApparentTemperature = Series(h, "apparent_temperature", n),
            Humidity = Series(h, "relative_humidity_2m", n),
            Precipitation = Series(h, "precipitation", n),
            Snowfall = Series(h, "snowfall", n),
            CloudCover = Series(h, "cloud_cover", n),
            CloudLow = Series(h, "cloud_cover_low", n),
            CloudMid = Series(h, "cloud_cover_mid", n),
            CloudHigh = Series(h, "cloud_cover_high", n),
            Pressure = Series(h, "pressure_msl", n),
            WindSpeed = Series(h, "wind_speed_10m", n),
            WindDirection = Series(h, "wind_direction_10m", n),
            WindGusts = Series(h, "wind_gusts_10m", n),
            IsDay = Series(h, "is_day", n).Select(v => v > 0.5).ToArray(),
        };

        DailySeries daily;
        if (root.TryGetProperty("daily", out var d))
        {
            var dates = Times(d.GetProperty("time"));
            var m = dates.Length;
            daily = new DailySeries
            {
                Date = dates,
                TempMax = Series(d, "temperature_2m_max", m),
                TempMin = Series(d, "temperature_2m_min", m),
                PrecipSum = Series(d, "precipitation_sum", m),
                WindMax = Series(d, "wind_speed_10m_max", m),
                GustMax = Series(d, "wind_gusts_10m_max", m),
                Sunrise = NullableTimes(d, "sunrise", m),
                Sunset = NullableTimes(d, "sunset", m),
            };
        }
        else
        {
            daily = new DailySeries();
        }

        return new ForecastResult { Place = place, Hourly = hourly, Daily = daily, FetchedUtc = fetchedUtc, Model = Model };
    }

    private static DateTime[] Times(JsonElement arr) =>
        arr.EnumerateArray()
           .Select(e => DateTime.Parse(e.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.None))
           .ToArray();

    private static JsonElement? Find(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var exact) && exact.ValueKind == JsonValueKind.Array) return exact;
        // Při dotazu na více modelů API přidává příponu s názvem modelu.
        if (obj.TryGetProperty(name + "_" + Model, out var suffixed) && suffixed.ValueKind == JsonValueKind.Array) return suffixed;
        return null;
    }

    private static double[] Series(JsonElement obj, string name, int n)
    {
        var result = new double[n];
        Array.Fill(result, double.NaN);
        var arr = Find(obj, name);
        if (arr is null) return result;
        var i = 0;
        foreach (var e in arr.Value.EnumerateArray())
        {
            if (i >= n) break;
            if (e.ValueKind == JsonValueKind.Number) result[i] = e.GetDouble();
            i++;
        }
        return result;
    }

    private static DateTime?[] NullableTimes(JsonElement obj, string name, int n)
    {
        var result = new DateTime?[n];
        var arr = Find(obj, name);
        if (arr is null) return result;
        var i = 0;
        foreach (var e in arr.Value.EnumerateArray())
        {
            if (i >= n) break;
            if (e.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                result[i] = t;
            i++;
        }
        return result;
    }
}
