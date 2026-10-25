using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Meteo.Core;
using Microsoft.Win32;

namespace Meteo.Windows;

/// <summary>
/// Samostatné okno přes jeden monitor (spuštění Meteo.exe):
///  - není topmost, může ho překrýt jiné okno, je na hlavním panelu,
///  - nejde měnit velikost ani polohu, maximalizovat, připnout (Win+šipky) – hlídá to <see cref="FullscreenGuard"/>,
///  - Win+Shift+Vlevo / Vpravo ho přesune na sousední monitor (zleva doprava, dokola),
///  - Vlevo / Vpravo = předchozí / další místo, Esc (nebo Alt+F4) = konec.
/// </summary>
internal sealed class FullscreenWindow : Window
{
    private readonly AppServices _s;
    private readonly DashboardHost _host;
    private readonly IScreenProvider _screens;
    private readonly FullscreenGuard _guard = new();
    private ShellKeyHook? _hook;
    private ScreenInfo _current;

    public FullscreenWindow(AppServices services, DashboardHost host, Dashboard dashboard, IScreenProvider screens, ScreenInfo screen)
    {
        _s = services;
        _host = host;
        _screens = screens;
        _current = screen;
        _guard.Target = screen;

        Title = "Meteo";
        Background = Brushes.Black;
        Content = dashboard;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Přibližné hodnoty (DIP); přesné pixely nastaví WindowPlacement.
        Left = screen.X;
        Top = screen.Y;
        Width = screen.Width;
        Height = screen.Height;

        SourceInitialized += OnSourceInitialized;
        ContentRendered += (_, _) => Place();
        StateChanged += OnStateChanged;
        PreviewKeyDown += OnKeyDown;
        Closed += OnClosed;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        HwndSource.FromHwnd(Handle)?.AddHook(_guard.WndProc);
        Place();

        _hook = new ShellKeyHook(Handle);
        _hook.MoveRequested += dir => Dispatcher.InvokeAsync(() => MoveToNeighbour(dir));
    }

    private void Place() => WindowPlacement.CoverScreen(Handle, _current, topmost: false);

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // Maximalizaci blokuje guard; kdyby se přesto objevila, vrať okno do normálu. Po obnovení z minimalizace srovnej polohu.
        if (WindowState != WindowState.Normal && WindowState != WindowState.Minimized) WindowState = WindowState.Normal;
        if (WindowState == WindowState.Normal) Place();
    }

    private List<ScreenInfo> OrderedScreens() =>
        _screens.GetScreens().OrderBy(s => s.X).ThenBy(s => s.Y).ToList();

    private void MoveToNeighbour(int direction)
    {
        var list = OrderedScreens();
        if (list.Count < 2) return;

        var i = list.FindIndex(s => s.Name == _current.Name);
        if (i < 0) i = 0;
        var next = list[((i + direction) % list.Count + list.Count) % list.Count];

        _current = next;
        _guard.Target = next;   // nejdřív cíl, pak přesun – jinak by ho guard vrátil zpět
        Place();

    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(() =>
        {
            // Monitor se odpojil / změnilo se rozlišení: najdi ten náš podle názvu, jinak primární.
            var list = OrderedScreens();
            var again = list.FirstOrDefault(s => s.Name == _current.Name) ?? list.FirstOrDefault(s => s.IsPrimary) ?? list[0];
            _current = again;
            _guard.Target = again;
            Place();
        });

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Right:
                _host.Next();
                e.Handled = true;
                break;
            case Key.Left:
                _host.Previous();
                e.Handled = true;
                break;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hook?.Dispose();
        _hook = null;
    }
}
