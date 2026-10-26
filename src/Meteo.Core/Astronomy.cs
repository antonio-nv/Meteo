namespace Meteo.Core;

/// <summary>Poloha tělesa na obloze: azimut od severu po směru hodinových ručiček (0° = S, 90° = V), výška nad obzorem.</summary>
public readonly record struct SkyPosition(double Azimuth, double Elevation);

/// <summary>Měsíc: poloha, osvětlená část kotouče (0 = nov, 100 = úplněk) a zda dorůstá.</summary>
public readonly record struct MoonInfo(SkyPosition Position, double IlluminationPercent, bool Waxing, double DistanceKm);

/// <summary>
/// Poloha Slunce a Měsíce, fáze Měsíce a časy východu / západu. Počítá se přímo z polohy a času (bez stahování dat):
/// Slunce podle vzorců NOAA, Měsíc zkrácenou řadou podle Meeuse (Astronomical Algorithms, kap. 47) s topocentrickou
/// opravou. Ověřeno proti knihovně PyEphem: polohy se shodují na asi 0,02°, časy východů a západů do 1–2 minut.
/// Výsledek závisí na zeměpisné šířce a délce místa; u Měsíce se navíc projevuje paralaxa (až asi 1°), protože je blízko.
/// </summary>
public static class Astronomy
{
    private const double Deg = Math.PI / 180.0;

    private static double Norm(double degrees)
    {
        var d = degrees % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    private static double JulianDay(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Subtract(DateTime.UnixEpoch).TotalDays + 2440587.5;

    /// <summary>Sklon ekliptiky v stupních (T = století od J2000).</summary>
    private static double Obliquity(double t) =>
        23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0;

    private readonly record struct Equatorial(double RaDeg, double DecDeg, double EclipticLonDeg, double DistanceKm);

    private static Equatorial SunEquatorial(double jd)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var l0 = Norm(280.46646 + t * (36000.76983 + t * 0.0003032));
        var m = Norm(357.52911 + t * (35999.05029 - 0.0001537 * t));
        var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        var mr = m * Deg;
        var c = Math.Sin(mr) * (1.914602 - t * (0.004817 + 0.000014 * t))
                + Math.Sin(2 * mr) * (0.019993 - 0.000101 * t)
                + Math.Sin(3 * mr) * 0.000289;
        var trueLon = l0 + c;
        var nu = (m + c) * Deg;
        var rAu = 1.000001018 * (1 - e * e) / (1 + e * Math.Cos(nu));
        var omega = 125.04 - 1934.136 * t;
        var lambda = (trueLon - 0.00569 - 0.00478 * Math.Sin(omega * Deg)) * Deg;
        var eps = (Obliquity(t) + 0.00256 * Math.Cos(omega * Deg)) * Deg;
        var ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda)) / Deg;
        var dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda)) / Deg;
        return new Equatorial(Norm(ra), dec, Norm(lambda / Deg), rAu * 149597870.7);
    }

    /// <summary>Z rovníkových souřadnic (stupně) a místa na azimut a výšku bez refrakce (geometrická výška středu tělesa).</summary>
    private static SkyPosition Horizontal(double jd, double raDeg, double decDeg, double latitude, double longitude)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var gmst = Norm(280.46061837 + 360.98564736629 * (jd - 2451545.0) + 0.000387933 * t * t);
        var h = Norm(gmst + longitude - raDeg) * Deg;
        var phi = latitude * Deg;
        var dec = decDeg * Deg;
        var sinAlt = Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(h);
        var alt = Math.Asin(Math.Clamp(sinAlt, -1, 1));
        var azSouth = Math.Atan2(Math.Sin(h), Math.Cos(h) * Math.Sin(phi) - Math.Tan(dec) * Math.Cos(phi));
        return new SkyPosition(Norm(azSouth / Deg + 180.0), alt / Deg);
    }

    /// <summary>Zdánlivá výška: přičte lom světla v atmosféře (Bennett, standardní tlak a teplota).</summary>
    public static double ApparentElevation(double geometricElevation)
    {
        if (geometricElevation < -1.0) return geometricElevation;
        var h = geometricElevation;
        var refractionArcMin = 1.0 / Math.Tan((h + 7.31 / (h + 4.4)) * Deg);
        return h + refractionArcMin / 60.0;
    }

    /// <summary>Slunce: geometrická výška středu (bez lomu), pro výpočet východů a západů.</summary>
    public static SkyPosition SunGeometric(DateTime utc, double latitude, double longitude)
    {
        var jd = JulianDay(utc);
        var s = SunEquatorial(jd);
        return Horizontal(jd, s.RaDeg, s.DecDeg, latitude, longitude);
    }

    /// <summary>Slunce pro zobrazení: azimut a zdánlivá výška (s lomem světla).</summary>
    public static SkyPosition Sun(DateTime utc, double latitude, double longitude)
    {
        var p = SunGeometric(utc, latitude, longitude);
        return p with { Elevation = ApparentElevation(p.Elevation) };
    }

    // Členy řady Měsíce: násobky D, M, M', F a koeficient (longitudy v 1e-6 °, vzdálenosti v 1e-3 km).
    private static readonly (int D, int M, int Mp, int F, double L, double R)[] LonDist =
    [
        (0, 0, 1, 0, 6288774, -20905355), (2, 0, -1, 0, 1274027, -3699111), (2, 0, 0, 0, 658314, -2955968),
        (0, 0, 2, 0, 213618, -569925), (0, 1, 0, 0, -185116, 48888), (0, 0, 0, 2, -114332, -3149),
        (2, 0, -2, 0, 58793, 246158), (2, -1, -1, 0, 57066, -152138), (2, 0, 1, 0, 53322, -170733),
        (2, -1, 0, 0, 45758, -204586), (0, 1, -1, 0, -40923, -129620), (1, 0, 0, 0, -34720, 108743),
        (0, 1, 1, 0, -30383, 104755), (2, 0, 0, -2, 15327, 10321), (0, 0, 1, 2, -12528, 0),
        (0, 0, 1, -2, 10980, 79661), (4, 0, -1, 0, 10675, -34782), (0, 0, 3, 0, 10034, -23210),
        (4, 0, -2, 0, 8548, -21636), (2, 1, -1, 0, -7888, 24208), (2, 1, 0, 0, -6766, 30824),
        (1, 0, -1, 0, -5163, -8379), (1, 1, 0, 0, 4987, -16675), (2, -1, 1, 0, 4036, -12831),
        (2, 0, 2, 0, 3994, -10445), (4, 0, 0, 0, 3861, -11650), (2, 0, -3, 0, 3665, 14403),
        (0, 1, -2, 0, -2689, -7003), (2, 0, -1, 2, -2602, 0), (2, -1, -2, 0, 2390, 10056),
        (1, 0, 1, 0, -2348, 6322), (2, -2, 0, 0, 2236, -9884),
    ];

    private static readonly (int D, int M, int Mp, int F, double B)[] Lat =
    [
        (0, 0, 0, 1, 5128122), (0, 0, 1, 1, 280602), (0, 0, 1, -1, 277693), (2, 0, 0, -1, 173237),
        (2, 0, -1, 1, 55413), (2, 0, -1, -1, 46271), (2, 0, 0, 1, 32573), (0, 0, 2, 1, 17198),
        (2, 0, 1, -1, 9266), (0, 0, 2, -1, 8822), (2, -1, 0, -1, 8216), (2, 0, -2, -1, 4324),
        (2, 0, 1, 1, 4200), (2, 1, 0, -1, -3359),
    ];

    private static (double LonDeg, double LatDeg, double DistKm) MoonEcliptic(double jd)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var lp = Norm(218.3164477 + 481267.88123421 * t);
        var d = Norm(297.8501921 + 445267.1114034 * t) * Deg;
        var m = Norm(357.5291092 + 35999.0502909 * t) * Deg;
        var mp = Norm(134.9633964 + 477198.8675055 * t) * Deg;
        var f = Norm(93.2720950 + 483202.0175233 * t) * Deg;
        var e = 1 - 0.002516 * t - 0.0000074 * t * t;

        double sumL = 0, sumR = 0, sumB = 0;
        foreach (var (cd, cm, cmp, cf, l, r) in LonDist)
        {
            var arg = cd * d + cm * m + cmp * mp + cf * f;
            var ef = Math.Abs(cm) == 1 ? e : Math.Abs(cm) == 2 ? e * e : 1;
            sumL += ef * l * Math.Sin(arg);
            sumR += ef * r * Math.Cos(arg);
        }
        foreach (var (cd, cm, cmp, cf, b) in Lat)
        {
            var arg = cd * d + cm * m + cmp * mp + cf * f;
            var ef = Math.Abs(cm) == 1 ? e : Math.Abs(cm) == 2 ? e * e : 1;
            sumB += ef * b * Math.Sin(arg);
        }

        var a1 = Norm(119.75 + 131.849 * t) * Deg;
        var a2 = Norm(53.09 + 479264.290 * t) * Deg;
        var a3 = Norm(313.45 + 481266.484 * t) * Deg;
        var lpr = lp * Deg;
        sumL += 3958 * Math.Sin(a1) + 1962 * Math.Sin(lpr - f) + 318 * Math.Sin(a2);
        sumB += -2235 * Math.Sin(lpr) + 382 * Math.Sin(a3) + 175 * Math.Sin(a1 - f) + 175 * Math.Sin(a1 + f)
                + 127 * Math.Sin(lpr - mp) - 115 * Math.Sin(lpr + mp);

        return (Norm(lp + sumL / 1e6), sumB / 1e6, 385000.56 + sumR / 1000.0);
    }

    /// <summary>Měsíc: topocentrická poloha (s paralaxou), zdánlivá výška, fáze a vzdálenost.</summary>
    public static MoonInfo Moon(DateTime utc, double latitude, double longitude)
    {
        var jd = JulianDay(utc);
        var (g, p) = MoonGeometricAndPhase(jd, latitude, longitude);
        return new MoonInfo(g with { Elevation = ApparentElevation(g.Elevation) }, p.Percent, p.Waxing, p.Dist);
    }

    /// <summary>Měsíc: topocentrická výška středu (bez lomu), pro výpočet východů a západů.</summary>
    public static SkyPosition MoonGeometric(DateTime utc, double latitude, double longitude) =>
        MoonGeometricAndPhase(JulianDay(utc), latitude, longitude).Pos;

    private static (SkyPosition Pos, (double Percent, bool Waxing, double Dist) Phase) MoonGeometricAndPhase(
        double jd, double latitude, double longitude) => (MoonTopocentric(jd, latitude, longitude), MoonPhase(jd));

    private static SkyPosition MoonTopocentric(double jd, double latitude, double longitude)
    {
        var t = (jd - 2451545.0) / 36525.0;
        var (lon, lat, dist) = MoonEcliptic(jd);
        var eps = Obliquity(t) * Deg;
        var lr = lon * Deg;
        var br = lat * Deg;
        var ra = Math.Atan2(Math.Sin(lr) * Math.Cos(eps) - Math.Tan(br) * Math.Sin(eps), Math.Cos(lr)) / Deg;
        var dec = Math.Asin(Math.Sin(br) * Math.Cos(eps) + Math.Cos(br) * Math.Sin(eps) * Math.Sin(lr)) / Deg;
        var geo = Horizontal(jd, Norm(ra), dec, latitude, longitude);

        // Paralaxa: Měsíc je blízko, takže se z povrchu Země promítá níže, než by byl vidět ze středu Země.
        var parallax = Math.Asin(6378.14 / dist);
        var altitude = geo.Elevation * Deg;
        // Přesněji: h' = h − π·cos h (stačí pro π ≈ 1°), lom je až ve ApparentElevation.
        var topo = altitude - parallax * Math.Cos(altitude);
        return geo with { Elevation = topo / Deg };
    }

    private static (double Percent, bool Waxing, double Dist) MoonPhase(double jd)
    {
        var (lon, lat, dist) = MoonEcliptic(jd);
        var sun = SunEquatorial(jd);
        var dl = (lon - sun.EclipticLonDeg) * Deg;
        var cosPsi = Math.Cos(lat * Deg) * Math.Cos(dl);
        var psi = Math.Acos(Math.Clamp(cosPsi, -1, 1));
        var i = Math.Atan2(sun.DistanceKm * Math.Sin(psi), dist - sun.DistanceKm * cosPsi);
        var k = (1 + Math.Cos(i)) / 2;
        return (k * 100.0, Math.Sin(dl) > 0, dist);
    }

    /// <summary>Výška středu tělesa, při které horní okraj právě mizí / vychází (lom světla 34' + poloměr kotouče asi 16').</summary>
    public const double HorizonElevation = -0.833;

    /// <summary>
    /// Východ a západ tělesa v místním dni (jako lokální čas). Hledá se průchod výšky přes <see cref="HorizonElevation"/>
    /// po pěti minutách a zpřesňuje půlením. Mimo polární oblasti může u Měsíce jeden z časů v daném dni chybět (null).
    /// </summary>
    public static (DateTime? Rise, DateTime? Set) RiseSet(
        DateOnly day, Func<DateTime, SkyPosition> position, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var startLocal = day.ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal.AddDays(1), zone);

        double F(DateTime utc) => position(utc).Elevation - HorizonElevation;

        DateTime? rise = null, set = null;
        var step = TimeSpan.FromMinutes(5);
        var a = startUtc;
        var fa = F(a);
        while (a < endUtc)
        {
            var b = a + step;
            var fb = F(b);
            if ((fa < 0) != (fb < 0))
            {
                var lo = a; var hi = b; var flo = fa;
                for (var n = 0; n < 20; n++)
                {
                    var mid = lo + (hi - lo) / 2;
                    var fm = F(mid);
                    if ((fm < 0) == (flo < 0)) { lo = mid; flo = fm; } else hi = mid;
                }
                var when = TimeZoneInfo.ConvertTimeFromUtc(lo + (hi - lo) / 2, zone);
                if (fb >= 0) rise ??= when; else set ??= when;
            }
            a = b; fa = fb;
        }
        return (rise, set);
    }

    public static (DateTime? Rise, DateTime? Set) SunRiseSet(DateOnly day, double latitude, double longitude, TimeZoneInfo? zone = null) =>
        RiseSet(day, utc => SunGeometric(utc, latitude, longitude), zone);

    public static (DateTime? Rise, DateTime? Set) MoonRiseSet(DateOnly day, double latitude, double longitude, TimeZoneInfo? zone = null) =>
        RiseSet(day, utc => MoonGeometric(utc, latitude, longitude), zone);

    /// <summary>Slovní popis fáze podle osvětlení a směru: nov, dorůstá, první čtvrť, úplněk…</summary>
    public static string PhaseName(double percent, bool waxing) => percent switch
    {
        < 2 => "nov",
        > 98 => "úplněk",
        >= 48 and <= 52 => waxing ? "první čtvrť" : "poslední čtvrť",
        _ => waxing ? "dorůstá" : "couvá",
    };
}
