using System.Net.Http;
using Meteo.Core;
using Meteo.Windows;

namespace Meteo.Wpf;

/// <summary>Jednoduchý kontejner služeb (bez DI frameworku – aplikace je malá).</summary>
internal sealed class AppServices
{
    public required SettingsStore Store { get; init; }
    public required AppSettings Settings { get; init; }
    public required PlaceCatalog Catalog { get; init; }
    public required ForecastService Forecasts { get; init; }
    public required FlymetClient Flymet { get; init; }
    public required GeocodingClient Geocoding { get; init; }

    public string DataDir => Store.DataDirectory;

    public static AppServices Create()
    {
        var store = new SettingsStore(WindowsPaths.ResolveDataDirectory());
        var settings = store.Load();

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Meteo/1.0 (osobni sporic obrazovky)");

        return new AppServices
        {
            Store = store,
            Settings = settings,
            Catalog = new PlaceCatalog(settings),
            Forecasts = new ForecastService(new OpenMeteoClient(http), store.DataDirectory),
            Flymet = new FlymetClient(http, store.DataDirectory),
            Geocoding = new GeocodingClient(http),
        };
    }

    public void Log(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(DataDir, "error.log"), $"{DateTime.Now:s} {ex}{Environment.NewLine}"); }
        catch { /* logování nesmí shodit šetřič */ }
    }
}
