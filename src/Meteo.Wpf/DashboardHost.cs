using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Meteo.Core;

namespace Meteo.Wpf;

/// <summary>Co se má právě zobrazit.</summary>
public sealed class DashboardState
{
    public required Place Place { get; init; }
    public ForecastResult? Forecast { get; init; }
    public BitmapSource? Flymet { get; init; }
    public string Status { get; init; } = "";
    public bool IsError { get; init; }
    public bool IsStale { get; init; }
}

/// <summary>Střídá místa, stahuje data na pozadí a hlásí hotový stav všem oknům (monitorům).</summary>
internal sealed class DashboardHost
{
    private readonly AppServices _s;
    private readonly DispatcherTimer _timer = new();
    private CancellationTokenSource? _cts;
    private int _index = -1;

    public DashboardState? Current { get; private set; }
    public event Action<DashboardState>? Updated;

    public DashboardHost(AppServices services) => _s = services;

    public void Start()
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(10, _s.Settings.RotateSeconds));
        _timer.Tick += async (_, _) => await ShowAsync(1);
        _timer.Start();
        _ = ShowAsync(1);
    }

    /// <summary>Ručně: další místo (a časovač střídání začne znovu).</summary>
    public void Next() => Step(1);

    /// <summary>Ručně: předchozí místo.</summary>
    public void Previous() => Step(-1);

    /// <summary>Znovu zobrazí aktuální místo (např. po změně nastavení).</summary>
    public void Refresh() => Step(0);

    private void Step(int step)
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(10, _s.Settings.RotateSeconds));
        _timer.Stop();
        _timer.Start();
        _ = ShowAsync(step);
    }

    private async Task ShowAsync(int step)
    {
        var places = _s.Catalog.Selected();
        _index = (((_index + step) % places.Count) + places.Count) % places.Count;
        var place = places[_index];

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        if (Current is null)
            Publish(new DashboardState { Place = place, Status = "Načítám předpověď…" });

        try
        {
            var settings = _s.Settings;
            var (forecast, stale) = await _s.Forecasts.GetAsync(
                place, TimeSpan.FromMinutes(Math.Max(5, settings.RefreshMinutes)), ct);

            BitmapSource? image = null;
            if (settings.ShowFlymet)
            {
                try
                {
                    var bytes = await _s.Flymet.GetAsync(place, settings.FlymetSlugs, TimeSpan.FromHours(3), ct);
                    if (bytes is not null) image = Decode(bytes);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _s.Log(ex); }
            }

            if (ct.IsCancellationRequested) return;
            Publish(new DashboardState
            {
                Place = place,
                Forecast = forecast,
                Flymet = image,
                IsStale = stale,
                Status = stale ? "offline – zobrazena naposledy uložená předpověď" : "",
            });
        }
        catch (OperationCanceledException)
        {
            // přepnuto na další město
        }
        catch (Exception ex)
        {
            _s.Log(ex);
            Publish(new DashboardState
            {
                Place = place,
                Forecast = Current?.Forecast?.Place == place ? Current.Forecast : null,
                Status = "Chyba: " + ex.Message,
                IsError = true,
            });
        }
    }

    private void Publish(DashboardState state)
    {
        Current = state;
        Updated?.Invoke(state);
    }

    private static BitmapSource? Decode(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
