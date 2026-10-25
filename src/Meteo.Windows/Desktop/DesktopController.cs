using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Meteo.Core;
using Microsoft.Win32;

namespace Meteo.Windows;

/// <summary>
/// Řídí živé pozadí (/d): okno na každém monitoru zavěšené do plochy, ikona u hodin s nabídkou a klávesový hák
/// pro přepínání míst. Okna se po změně monitorů nebo restartu Průzkumníka vytvoří znovu.
/// </summary>
internal sealed class DesktopController : IDisposable
{
    private readonly AppServices _s;
    private readonly DashboardHost _host;
    private readonly FontFamily _family;
    private readonly IScreenProvider _screens;
    private readonly string? _variant;
    private readonly bool _diagnostics;
    private bool _building;
    private readonly List<(DesktopWindow Window, Dashboard? Dash)> _windows = new();
    private readonly DispatcherTimer _rebuildTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private AppTray? _tray;
    private DesktopKeyHook? _keys;
    private bool _disposed;

    public DesktopController(AppServices services, DashboardHost host, FontFamily family, IScreenProvider screens, string? variant, bool diagnostics = false)
    {
        _s = services;
        _host = host;
        _family = family;
        _screens = screens;
        _variant = variant;
        _diagnostics = diagnostics;
    }

    public void Start()
    {
        var exe = Environment.ProcessPath;
        _s.Log($"Start živého pozadí (/d{(_variant is null ? "" : ":" + _variant)}), sestavení {(exe is null ? "?" : File.GetLastWriteTime(exe).ToString("s"))}, kreslení {System.Windows.Media.RenderOptions.ProcessRenderMode}");
        Build();

        _tray = new AppTray(_s, _host, "Živé pozadí", "Win+D", desktopMode: true);
        _tray.ShellRestarted += ScheduleRebuild;

        _keys = new DesktopKeyHook();
        _keys.StepRequested += dir => Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (dir < 0) _host.Previous(); else _host.Next();
        });
        // Win+D (zobrazit plochu) Meteo ukončí, aby po něm zůstala obyčejná plocha s ikonami.
        _keys.ShowDesktopPressed += () => Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
        if (!_keys.IsInstalled) _s.Log("Klávesový hák se nepodařilo nainstalovat; šipky na ploše nebudou fungovat.");

        _rebuildTimer.Tick += (_, _) => { _rebuildTimer.Stop(); Build(); };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Application.Current.Exit += (_, _) => Dispose();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.InvokeAsync(() => ScheduleRebuild());

    /// <summary>Počká chvíli (Průzkumník po změně nebo restartu potřebuje čas na nová okna) a okna znovu vytvoří.</summary>
    private void ScheduleRebuild()
    {
        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    private void Build()
    {
        _building = true;
        try { BuildCore(); }
        finally { _building = false; }
    }

    private void BuildCore()
    {
        Teardown();
        foreach (var screen in _screens.GetScreens())
        {
            Dashboard? dash = null;
            if (screen.IsPrimary || _s.Settings.AllMonitors)
            {
                dash = new Dashboard(_s.Settings, _family) { Hint = "Meteo · Win+D = konec" };
                _host.Updated += dash.SetState;
                _host.RadarUpdated += dash.SetRadar;
                if (_host.Current is { } state) dash.SetState(state);
                if (_host.CurrentRadar.Count > 0) dash.SetRadar(_host.CurrentRadar);
            }
            var window = new DesktopWindow(dash, screen, _variant, _host, _s.Log, _diagnostics);
            // Okno minimalizované tlačítkem "Zobrazit plochu" nebo Win+M: Meteo se ukončí (plocha zůstane s ikonami).
            window.Minimized += () =>
            {
                if (!_building && !_disposed) Application.Current.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
            };
            window.Show();
            _windows.Add((window, dash));
        }
    }

    private void Teardown()
    {
        foreach (var (window, dash) in _windows)
        {
            if (dash is not null)
            {
                _host.Updated -= dash.SetState;
                _host.RadarUpdated -= dash.SetRadar;
            }
            window.Content = null;
            window.Close();
        }
        _windows.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _rebuildTimer.Stop();
        _keys?.Dispose();
        _tray?.Dispose();
        Teardown();
        WallpaperHost.RefreshWallpaper();
    }
}
