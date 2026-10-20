namespace Meteo.Core;

/// <summary>Monitor v pixelech virtuální plochy.</summary>
public sealed record ScreenInfo(string Name, int X, int Y, int Width, int Height, bool IsPrimary);

/// <summary>Abstrakce nad OS: každá platforma (Windows 11, později Linux Mint) dodá vlastní implementaci.</summary>
public interface IScreenProvider
{
    IReadOnlyList<ScreenInfo> GetScreens();
}
