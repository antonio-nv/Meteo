using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>
/// Okno živého pozadí na jednom monitoru. Po zobrazení se zavěsí do plochy (<see cref="WallpaperHost"/>).
/// Když se zavěšení nepovede, zůstane obyčejné okno pod všemi ostatními (bez tlačítka na liště a v Alt+Tab).
/// </summary>
internal sealed class DesktopWindow : Window
{
    private bool _attached;

    public ScreenInfo Screen { get; }

    /// <summary>Výsledek zavěšení do plochy (null, dokud okno není zobrazené).</summary>
    public AttachResult? Result { get; private set; }

    /// <summary>Okno bylo minimalizováno (Win+D, tlačítko Zobrazit plochu, Win+M).</summary>
    public event Action? Minimized;

    public DesktopWindow(Dashboard? dashboard, ScreenInfo screen, string? variant, DashboardHost host, Action<string> log,
        bool diagnostics = false)
    {
        Screen = screen;
        Title = "Meteo";
        Background = Brushes.Black;
        Content = dashboard;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Přibližně; přesné pixely nastaví WallpaperHost.
        Left = screen.X;
        Top = screen.Y;
        Width = screen.Width;
        Height = screen.Height;

        ContentRendered += (_, _) =>
        {
            if (_attached) return;
            _attached = true;
            var handle = new WindowInteropHelper(this).Handle;
            Result = WallpaperHost.Attach(handle, screen, variant);
            log(Result.Detail);

            // Ladění: stav oken hned po zavěšení a po 3 s (kdyby okno zmizelo nebo se přesunulo).
            if (diagnostics && screen.IsPrimary)
            {
                log("Okna plochy po zavěšení:\n" + DesktopDiagnostics.Dump(handle));
                var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                later.Tick += (_, _) =>
                {
                    later.Stop();
                    log("Okna plochy po 3 s:\n" + DesktopDiagnostics.Dump(handle));
                };
                later.Start();
            }
        };

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized) Minimized?.Invoke();
        };

        // Funguje jen v nouzovém režimu (okno pod ostatními), kdy se na ně dá kliknout a mají fokus.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Right) { host.Next(); e.Handled = true; }
            else if (e.Key == Key.Left) { host.Previous(); e.Handled = true; }
        };
    }
}
