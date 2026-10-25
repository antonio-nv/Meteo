using System.Windows;
using Meteo.Core;

namespace Meteo.Windows;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var parsed = StartupArgs.Parse(args);

        // Živé pozadí běží jen jednou: druhé spuštění (např. po ručním startu, když už naběhlo s Windows) se tiše ukončí.
        var firstInstance = true;
        using var singleInstance = parsed.Mode == StartMode.Desktop
            ? new Mutex(true, @"Local\Meteo.Desktop", out firstInstance)
            : null;
        if (!firstInstance) return 0;

        var services = AppServices.Create();
        var app = new Application();

        // "/d:sw" = softwarové kreslení WPF (jinak grafickou kartou).
        if (parsed.Mode == StartMode.Desktop && parsed.DesktopOptions().Software)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

        app.DispatcherUnhandledException += (_, e) =>
        {
            services.Log(e.Exception);
            e.Handled = true; // program nesmí zůstat viset v chybovém dialogu
        };

        // Živé pozadí si okna při změně monitorů vytváří znovu, proto se ukončuje jen výslovně (Win+D, nabídka u hodin).
        app.ShutdownMode = parsed.Mode == StartMode.Desktop ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnLastWindowClose;
        app.Startup += (_, _) => OpenWindows(app, services, parsed);
        return app.Run();
    }

    private static void OpenWindows(Application app, AppServices services, StartupArgs parsed)
    {
        var family = FontResolver.Resolve(services.Settings, services.DataDir);
        var host = new DashboardHost(services);

        switch (parsed.Mode)
        {
            case StartMode.Desktop:
            {
                var (method, _, diagnostics) = parsed.DesktopOptions();
                new DesktopController(services, host, family, new WindowsScreenProvider(), method, diagnostics).Start();
                break;
            }

            case StartMode.Windowed:
            {
                var dash = new Dashboard(services.Settings, family) { Width = parsed.Width, Height = parsed.Height };
                host.Updated += dash.SetState;
                host.RadarUpdated += dash.SetRadar;
                new DebugWindow(dash).Show();
                break;
            }

            default: // StartMode.Fullscreen
            {
                var provider = new WindowsScreenProvider();
                var screens = provider.GetScreens().OrderBy(s => s.X).ThenBy(s => s.Y).ToList();
                var number = parsed.Monitor >= 1 ? parsed.Monitor : services.Settings.Monitor;
                var target =
                    (number >= 1 && number <= screens.Count ? screens[number - 1] : null)
                    ?? screens.FirstOrDefault(s => s.IsPrimary)
                    ?? screens[0];

                var dash = new Dashboard(services.Settings, family) { Hint = "Meteo · Esc = konec" };
                host.Updated += dash.SetState;
                host.RadarUpdated += dash.SetRadar;
                new FullscreenWindow(services, host, dash, provider, target).Show();

                var tray = new AppTray(services, host, "Samostatné okno", "Esc", desktopMode: false);
                app.Exit += (_, _) => tray.Dispose();
                break;
            }
        }

        host.Start();
    }
}
