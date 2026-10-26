namespace Meteo.Windows;

/// <summary>Kde leží konfigurace a cache na Windows.</summary>
public static class WindowsPaths
{
    /// <summary>
    /// Pořadí: 1) proměnná prostředí METEO_HOME, 2) %LOCALAPPDATA%\Meteo.
    /// Složka musí být zapisovatelná, jinak se přeskočí. Do složky s programem (Program Files) se nezapisuje.
    /// </summary>
    public static string ResolveDataDirectory()
    {
        var env = Environment.GetEnvironmentVariable("METEO_HOME");
        if (!string.IsNullOrWhiteSpace(env) && IsWritable(env)) return env;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Meteo");
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
