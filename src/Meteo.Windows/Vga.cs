using System.Windows.Media;

namespace Meteo.Windows;

/// <summary>Standardní 16barevná paleta VGA – drží vzhled v duchu fontu IBM VGA.</summary>
internal static class Vga
{
    private static SolidColorBrush B(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static readonly SolidColorBrush Black = B(0x00, 0x00, 0x00);
    public static readonly SolidColorBrush Blue = B(0x00, 0x00, 0xAA);
    public static readonly SolidColorBrush Green = B(0x00, 0xAA, 0x00);
    public static readonly SolidColorBrush Cyan = B(0x00, 0xAA, 0xAA);
    public static readonly SolidColorBrush Red = B(0xAA, 0x00, 0x00);
    public static readonly SolidColorBrush Magenta = B(0xAA, 0x00, 0xAA);
    public static readonly SolidColorBrush Brown = B(0xAA, 0x55, 0x00);
    public static readonly SolidColorBrush Gray = B(0xAA, 0xAA, 0xAA);
    public static readonly SolidColorBrush DarkGray = B(0x55, 0x55, 0x55);
    public static readonly SolidColorBrush LightBlue = B(0x55, 0x55, 0xFF);
    public static readonly SolidColorBrush LightGreen = B(0x55, 0xFF, 0x55);
    public static readonly SolidColorBrush LightCyan = B(0x55, 0xFF, 0xFF);
    public static readonly SolidColorBrush LightRed = B(0xFF, 0x55, 0x55);
    public static readonly SolidColorBrush LightMagenta = B(0xFF, 0x55, 0xFF);
    public static readonly SolidColorBrush Yellow = B(0xFF, 0xFF, 0x55);
    public static readonly SolidColorBrush White = B(0xFF, 0xFF, 0xFF);

    public static SolidColorBrush Shade(double fraction)
    {
        var v = (byte)Math.Round(0xAA * Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1));
        return B(v, v, v);
    }
}
