using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Meteo.Windows;

/// <summary>Ladicí okno běžné velikosti (/w ŠxV), aby šlo zkoušet rozvržení na různých rozlišeních. Esc ho zavře.</summary>
public sealed class DebugWindow : Window
{
    public DebugWindow(Dashboard dashboard)
    {
        Title = "Meteo (ladění) – Esc zavře";
        Background = Brushes.Black;
        Content = dashboard;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }
}
