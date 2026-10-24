using System.Windows;
using Meteo.Core;

namespace Meteo.Windows;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // .scr bez parametru = nastavení (konvence Windows); běžný .exe bez parametru = celoobrazovkové okno.
        var asScr = string.Equals(Path.GetExtension(Environment.ProcessPath), ".scr", StringComparison.OrdinalIgnoreCase);
        var parsed = ScreensaverArgs.Parse(args, asScr);

        // /p – náhled v malém okně nastavení Windows: nic nekreslíme, jen se tiše ukončíme.
        if (parsed.Mode == ScreensaverMode.Preview) return 0;

        var services = AppServices.Create();
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) =>
        {
            services.Log(e.Exception);
            e.Handled = true; // šetřič nesmí zůstat viset v chybovém dialogu
        };

        if (parsed.Mode == ScreensaverMode.Configure)
        {
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            return app.Run(new SettingsWindow(services));
        }

        app.ShutdownMode = ShutdownMode.OnLastWindowClose;
        app.Startup += (_, _) => OpenWindows(services, parsed);
        return app.Run();
    }

    private static void OpenWindows(AppServices services, ScreensaverArgs parsed)
    {
        var family = FontResolver.Resolve(services.Settings, services.DataDir);
        var host = new DashboardHost(services);

        if (parsed.Mode == ScreensaverMode.Fullscreen)
        {
            var provider = new WindowsScreenProvider();
            var screens = provider.GetScreens().OrderBy(s => s.X).ThenBy(s => s.Y).ToList();
            var target =
                (parsed.Monitor >= 1 && parsed.Monitor <= screens.Count ? screens[parsed.Monitor - 1] : null)
                ?? screens.FirstOrDefault(s => s.Name == services.Settings.LastMonitor)
                ?? screens.FirstOrDefault(s => s.IsPrimary)
                ?? screens[0];

            var dash = new Dashboard(services.Settings, family);
            host.Updated += dash.SetState;
            host.RadarUpdated += dash.SetRadar;
            new FullscreenWindow(services, host, dash, provider, target).Show();
        }
        else if (parsed.Mode == ScreensaverMode.Windowed)
        {
            var dash = new Dashboard(services.Settings, family) { Width = parsed.Width, Height = parsed.Height };
            host.Updated += dash.SetState;
            host.RadarUpdated += dash.SetRadar;
            new ScreensaverWindow(dash, null, windowed: true).Show();
        }
        else
        {
            foreach (var screen in new WindowsScreenProvider().GetScreens())
            {
                Dashboard? dash = null;
                if (screen.IsPrimary || services.Settings.AllMonitors)
                {
                    dash = new Dashboard(services.Settings, family);
                    host.Updated += dash.SetState;
                    host.RadarUpdated += dash.SetRadar;
                }
                new ScreensaverWindow(dash, screen, windowed: false).Show();
            }
        }

        host.Start();
    }
}
