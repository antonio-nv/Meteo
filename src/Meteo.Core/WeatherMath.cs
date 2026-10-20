namespace Meteo.Core;

/// <summary>Drobné meteorologické a grafové pomůcky bez závislosti na UI.</summary>
public static class WeatherMath
{
    private static readonly string[] Compass8 = ["S", "SV", "V", "JV", "J", "JZ", "Z", "SZ"];

    /// <summary>Světová strana, ODKUD vítr vane (česky: S, SV, V, JV, J, JZ, Z, SZ).</summary>
    public static string CompassName(double degrees)
    {
        if (double.IsNaN(degrees)) return "-";
        var d = ((degrees % 360) + 360) % 360;
        return Compass8[(int)Math.Round(d / 45.0) % 8];
    }

    private static readonly double[] BeaufortLimits = [0.3, 1.6, 3.4, 5.5, 8.0, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7];

    /// <summary>Beaufortova stupnice z rychlosti v m/s.</summary>
    public static int Beaufort(double ms)
    {
        if (double.IsNaN(ms)) return 0;
        for (var i = 0; i < BeaufortLimits.Length; i++)
            if (ms < BeaufortLimits[i]) return i;
        return 12;
    }

    /// <summary>Pěkná osa grafu: vrací zaokrouhlené minimum, maximum a krok.</summary>
    public static (double Min, double Max, double Step) NiceScale(double lo, double hi, int maxTicks)
    {
        if (double.IsNaN(lo) || double.IsNaN(hi)) return (0, 1, 1);
        if (!(hi > lo)) { lo -= 0.5; hi += 0.5; }
        var range = NiceNum(hi - lo, false);
        var step = NiceNum(range / Math.Max(1, maxTicks - 1), true);
        return (Math.Floor(lo / step) * step, Math.Ceiling(hi / step) * step, step);
    }

    private static double NiceNum(double x, bool round)
    {
        var exp = Math.Floor(Math.Log10(x));
        var f = x / Math.Pow(10, exp);
        double nf;
        if (round) nf = f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10;
        else nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        return nf * Math.Pow(10, exp);
    }

    /// <summary>Minimum a maximum ignorující NaN. Když nejsou data, vrací (NaN, NaN).</summary>
    public static (double Min, double Max) Range(IEnumerable<double> values)
    {
        double lo = double.NaN, hi = double.NaN;
        foreach (var v in values)
        {
            if (double.IsNaN(v)) continue;
            if (double.IsNaN(lo) || v < lo) lo = v;
            if (double.IsNaN(hi) || v > hi) hi = v;
        }
        return (lo, hi);
    }

    /// <summary>Index hodiny nejblíže zadanému času (nebo -1, když je řada prázdná).</summary>
    public static int NearestIndex(DateTime[] times, DateTime t)
    {
        if (times.Length == 0) return -1;
        var best = 0;
        var bestDiff = double.MaxValue;
        for (var i = 0; i < times.Length; i++)
        {
            var d = Math.Abs((times[i] - t).TotalMinutes);
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        return best;
    }
}
