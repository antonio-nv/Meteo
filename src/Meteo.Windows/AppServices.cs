using System.Net.Http;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>Jednoduchý kontejner služeb (bez DI frameworku – aplikace je malá).</summary>
internal sealed class AppServices
{
    public required AppSettings Settings { get; init; }
    public required string DataDir { get; init; }
    public required PlaceCatalog Catalog { get; init; }
    public required ForecastService Forecasts { get; init; }
    public required FlymetClient Flymet { get; init; }
    public required RadarClient Radar { get; init; }

    public static AppServices Create()
    {
        // Nastavení je přímo v kódu (AppSettings); složka na disku slouží jen pro cache, vlastní font a error.log.
        var dataDir = WindowsPaths.ResolveDataDirectory();
        Directory.CreateDirectory(dataDir);
        var settings = new AppSettings();

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Meteo/1.0 (osobni zobrazovac pocasi)");

        return new AppServices
        {
            Settings = settings,
            DataDir = dataDir,
            Catalog = new PlaceCatalog(settings),
            Forecasts = new ForecastService(new OpenMeteoClient(http), dataDir),
            Flymet = new FlymetClient(http, dataDir),
            Radar = new RadarClient(http, dataDir),
        };
    }

    public void Log(Exception ex) => Log(ex.ToString());

    public void Log(string message)
    {
        try { File.AppendAllText(Path.Combine(DataDir, "error.log"), $"{DateTime.Now:s} {message}{Environment.NewLine}"); }
        catch { /* logování nesmí shodit program */ }
    }
}
