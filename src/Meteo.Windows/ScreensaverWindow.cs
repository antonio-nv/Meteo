using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>Okno přes celý monitor (nebo ladicí okno). Zavře se klávesou, kliknutím nebo pohybem myši.</summary>
public sealed class ScreensaverWindow : Window
{
    private readonly bool _windowed;
    private readonly DateTime _opened = DateTime.UtcNow;
    private Point? _mouseStart;

    public ScreensaverWindow(Dashboard? dashboard, ScreenInfo? screen, bool windowed)
    {
        _windowed = windowed;
        Background = Brushes.Black;
        Content = dashboard;

        if (windowed)
        {
            Title = "Meteo (ladění) – Esc zavře";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.None;
            WindowStartupLocation = WindowStartupLocation.Manual;

            if (screen is not null)
            {
                // Okno zůstane přesně na svém monitoru (Win+šipky apod. nic nezmění). Stěhování mezi monitory
                // ve šetřiči nemá smysl: na každém monitoru je jedno okno a jakýkoli vstup šetřič ukončí.
                var guard = new FullscreenGuard { Target = screen };
                SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(guard.WndProc);

                // Přibližné hodnoty; přesné pixely nastaví WindowPlacement (WPF počítá v DIP).
                Left = screen.X;
                Top = screen.Y;
                Width = screen.Width;
                Height = screen.Height;
                SourceInitialized += (_, _) => WindowPlacement.CoverScreen(new WindowInteropHelper(this).Handle, screen);
                ContentRendered += (_, _) => WindowPlacement.CoverScreen(new WindowInteropHelper(this).Handle, screen);
            }
        }

        Loaded += (_, _) => Activate();
        PreviewKeyDown += OnKeyDown;
        PreviewMouseDown += (_, _) => { if (!_windowed) Quit(); };
        PreviewMouseMove += OnMouseMove;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_windowed && e.Key != Key.Escape) return;
        Quit();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_windowed) return;
        var pos = e.GetPosition(this);
        // Krátce po startu se pohyb ignoruje (Windows po spuštění často pošle falešný MouseMove).
        if (_mouseStart is null || (DateTime.UtcNow - _opened).TotalMilliseconds < 800)
        {
            _mouseStart = pos;
            return;
        }
        if (Math.Abs(pos.X - _mouseStart.Value.X) > 8 || Math.Abs(pos.Y - _mouseStart.Value.Y) > 8)
            Quit();
    }

    private static void Quit() => Application.Current.Shutdown();
}
