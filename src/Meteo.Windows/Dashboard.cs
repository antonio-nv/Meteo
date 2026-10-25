using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>
/// Celá obrazovka šetřiče kreslená ručně do DrawingContextu: hlavička, meteogram (teplota, srážky, vítr se šipkami, tlak,
/// oblačnost, den/noc), údaje "nyní", denní přehled a obrázek Flymet. Rozvržení se řídí buňkami 8x16 px
/// (znak fontu IBM VGA), takže vypadá stejně na 1366x768 i na otočených 1080x1920 / 1200x1920.
/// </summary>
public sealed class Dashboard : FrameworkElement
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    private readonly AppSettings _settings;
    private readonly Typeface _typeface;
    private readonly DispatcherTimer _clock;

    private DashboardState? _state;

    // radar
    private readonly DispatcherTimer _radarTimer;
    private IReadOnlyList<RadarFrameImage> _radar = [];
    private int _radarIndex;
    private int _radarHold;
    private bool _radarVisible;
    private MapData? _map;
    private (Rect Rect, StreamGeometry Borders, StreamGeometry Regions)? _mapCache;
    private double _ppd = 1.0;   // pixelů na DIP
    private int _scale = 1;      // celočíselné zvětšení buňky 8x16

    private string _hint = "";

    /// <summary>Text v pravém dolním rohu (název programu a způsob ukončení).</summary>
    public string Hint
    {
        get => _hint;
        set { _hint = value ?? ""; InvalidateVisual(); }
    }

    public Dashboard(AppSettings settings, FontFamily family)
    {
        _settings = settings;
        _typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        // Bez vyhlazování: ostré bitmapové znaky i čáry (nezávisle na nastavení ClearType v registrech).
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _clock.Tick += (_, _) => InvalidateVisual();
        _radarTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _radarTimer.Tick += OnRadarTick;

        Loaded += (_, _) => { _clock.Start(); _radarTimer.Start(); };
        Unloaded += (_, _) => { _clock.Stop(); _radarTimer.Stop(); };
    }

    public void SetRadar(IReadOnlyList<RadarFrameImage> frames)
    {
        _radar = frames;
        // Nové snímky: začni od prvního; po načtení se rovnou přehrává.
        _radarIndex = 0;
        _radarHold = 0;
        InvalidateVisual();
    }

    private void OnRadarTick(object? sender, EventArgs e)
    {
        if (!_radarVisible || _radar.Count < 2) return;
        if (_radarHold > 0) { _radarHold--; return; }

        _radarIndex++;
        if (_radarIndex >= _radar.Count) _radarIndex = 0;
        if (_radarIndex == _radar.Count - 1) _radarHold = 5;   // na posledním snímku chvíli počkej
        InvalidateVisual();
    }

    public void SetState(DashboardState state)
    {
        _state = state;
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ metrika

    private double CW => 8.0 * _scale / _ppd;
    private double CH => 16.0 * _scale / _ppd;
    private double Em => 16.0 * _scale / _ppd;

    private double Snap(double v) => Math.Round(v * _ppd) / _ppd;
    /// <summary>Střed pixelu: čára tloušťky 1 px na této souřadnici je ostrá.</summary>
    private double Mid(double v) => (Math.Floor(v * _ppd) + 0.5) / _ppd;

    private Pen MakePen(Brush brush, double pixels = 1, bool dotted = false)
    {
        var pen = new Pen(brush, pixels / _ppd)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat,
            LineJoin = PenLineJoin.Round,
        };
        if (dotted)
        {
            pen.DashStyle = new DashStyle(new double[] { 1, 3 }, 0);
            pen.DashCap = PenLineCap.Flat;
        }
        return pen;
    }

    private static string Ascii(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }

    private void Text(DrawingContext dc, string s, double x, double y, Brush brush, TextAlignment align = TextAlignment.Left)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (_settings.AsciiOnly) s = Ascii(s);
        var ft = new FormattedText(s, Cs, FlowDirection.LeftToRight, _typeface, Em, brush,
            new NumberSubstitution(), TextFormattingMode.Display, _ppd)
        {
            LineHeight = CH,
        };
        var w = ft.WidthIncludingTrailingWhitespace;
        if (align == TextAlignment.Right) x -= w;
        else if (align == TextAlignment.Center) x -= w / 2;
        dc.DrawText(ft, new Point(Snap(x), Snap(y)));
    }

    /// <summary>Vykreslí text a vrátí x za jeho koncem (předpoklad: znak = jedna buňka).</summary>
    private double TextAdvance(DrawingContext dc, string s, double x, double y, Brush brush)
    {
        Text(dc, s, x, y, brush);
        return x + s.Length * CW;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Cs) + s[1..];

    private static Brush WindBrush(double ms) =>
        double.IsNaN(ms) ? Vga.Gray :
        ms < 1.6 ? Vga.Gray :
        ms < 5.5 ? Vga.LightGreen :
        ms < 10.8 ? Vga.Yellow :
        ms < 17.2 ? Vga.LightRed : Vga.LightMagenta;

    private static string F(double v, string format) => double.IsNaN(v) ? "-" : v.ToString(format, Cs);

    // ------------------------------------------------------------------ hlavní kreslení

    protected override void OnRender(DrawingContext dc)
    {
        _ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double w = ActualWidth, h = ActualHeight;
        if (w < 16 || h < 16) return;

        _scale = _settings.UiScale > 0
            ? _settings.UiScale
            : Math.Max(1, (int)Math.Floor(Math.Min(w * _ppd / 800.0, h * _ppd / 640.0)));

        dc.DrawRectangle(Vga.Black, null, new Rect(0, 0, w, h));

        double cw = CW, ch = CH;
        var cols = (int)Math.Floor(w / cw);
        var rows = (int)Math.Floor(h / ch);
        if (cols < 60 || rows < 24) { Text(dc, "Okno je příliš malé", 4, 4, Vga.Gray); return; }

        var ox = Snap((w - cols * cw) / 2);
        var oy = Snap((h - rows * ch) / 2);

        DrawHeader(dc, ox, oy, cols);
        DrawFooter(dc, ox, oy + (rows - 1) * ch, cols);

        var st = _state;
        var body = new Rect(ox, oy + 2 * ch, cols * cw, (rows - 3) * ch);

        if (st?.Forecast is not { } fc)
        {
            var msg = st is null ? "Načítám předpověď…" : st.Status;
            Text(dc, msg, body.Left + body.Width / 2, body.Top + body.Height / 2 - ch / 2,
                st?.IsError == true ? Vga.LightRed : Vga.Gray, TextAlignment.Center);
            return;
        }

        Rect meteo, info, fly;
        var landscape = w > h;
        if (landscape)
        {
            var mw = Math.Floor(cols * 0.62) * cw;
            meteo = new Rect(body.Left, body.Top, mw, body.Height);
            var rx = body.Left + mw + cw;
            var rw = body.Right - rx;
            var infoH = Math.Min(19 * ch, body.Height);
            info = new Rect(rx, body.Top, rw, infoH);
            fly = new Rect(rx, body.Top + infoH + ch, rw, Math.Max(0, body.Height - infoH - ch));
        }
        else
        {
            var mh = Math.Floor(body.Height / ch * 0.52) * ch;
            meteo = new Rect(body.Left, body.Top, body.Width, mh);
            var infoH = Math.Min(11 * ch, body.Height - mh);
            info = new Rect(body.Left, body.Top + mh + ch, body.Width, infoH);
            var fy = info.Bottom + ch;
            fly = new Rect(body.Left, fy, body.Width, Math.Max(0, body.Bottom - fy));
        }

        DrawMeteogram(dc, meteo, fc);
        DrawInfo(dc, info, fc, landscape);
        DrawLower(dc, fly, st, landscape);
    }

    // ------------------------------------------------------------------ hlavička a patička

    private void DrawHeader(DrawingContext dc, double ox, double oy, int cols)
    {
        double cw = CW, ch = CH;
        var st = _state;
        var name = st?.Place.Title ?? "Meteo";
        Text(dc, " " + name.ToUpper(Cs), ox, oy, Vga.White);
        Text(dc, Cap(DateTime.Now.ToString("ddd d.M.yyyy HH:mm", Cs)) + " ", ox + cols * cw, oy, Vga.Yellow, TextAlignment.Right);

        string sub = " ČHMÚ ALADIN · předpověď na 72 hodin";
        if (st?.Forecast is { } f)
            sub += $" · stažena {f.FetchedUtc.ToLocalTime():d.M. HH:mm}";
        Text(dc, sub, ox, oy + ch, Vga.Gray);
        if (!string.IsNullOrEmpty(st?.Status) && st.Forecast != null)
            Text(dc, st.Status + " ", ox + cols * cw, oy + ch, st.IsError ? Vga.LightRed : Vga.Yellow, TextAlignment.Right);

        var y = Mid(oy + 2 * ch - 2 / _ppd);
        dc.DrawLine(MakePen(Vga.LightCyan), new Point(ox, y), new Point(ox + cols * cw, y));
    }

    private void DrawFooter(DrawingContext dc, double ox, double y, int cols)
    {
        var ly = Mid(y + 1 / _ppd);
        dc.DrawLine(MakePen(Vga.DarkGray), new Point(ox, ly), new Point(ox + cols * CW, ly));
        var text = " Zdroj: model ALADIN (ČHMÚ) přes Open-Meteo.com, CC BY 4.0" +
                   (_settings.ShowRadar ? " · radar ČHMÚ" : "") +
                   (_settings.ShowFlymet ? " · Flymet / Meteopress" : "");
        Text(dc, text, ox, y, Vga.DarkGray);

        // Pravý dolní roh: co je to za program a jak se ukončí (nastaví okno, např. "Meteo · Esc = konec").
        if (Hint.Length > 0 && cols > text.Length + Hint.Length + 2)
            Text(dc, Hint + " ", ox + cols * CW, y, Vga.Gray, TextAlignment.Right);
    }

    // ------------------------------------------------------------------ meteogram

    private double MapY(Rect plot, double min, double max, double v) =>
        Mid(plot.Bottom - (v - min) / (max - min) * plot.Height);

    private void DrawMeteogram(DrawingContext dc, Rect r, ForecastResult f)
    {
        var hs = f.Hourly;
        var n = hs.Count;
        if (n < 2) return;

        double cw = CW, ch = CH;
        var rows = (int)Math.Floor(r.Height / ch);
        if (rows < 22) return;

        var left = Snap(r.Left + 5 * cw);
        var right = Snap(r.Right - cw);
        var bw = (right - left) / n;
        double X(double idx) => left + (idx + 0.5) * bw;
        double RowY(double row) => r.Top + row * ch;

        var dark = MakePen(Vga.DarkGray);
        var dotted = MakePen(Vga.DarkGray, 1, true);
        var axis = MakePen(Vga.Gray);

        // --- časová osa: názvy dnů, hodiny, den/noc, oblačnost
        for (var i = 0; i < n; i++)
        {
            var t = hs.Time[i];
            if (t.Hour == 12)
                Text(dc, Cap(t.ToString("ddd d.M.", Cs)), X(i), RowY(0), Vga.White, TextAlignment.Center);
            if (t.Hour % 6 == 0)
                Text(dc, t.Hour.ToString("00", Cs), X(i), RowY(1), t.Hour == 0 ? Vga.White : Vga.Gray, TextAlignment.Center);
        }

        Text(dc, "den", left - cw * 0.5, RowY(2), Vga.Gray, TextAlignment.Right);
        Text(dc, "obl", left - cw * 0.5, RowY(3), Vga.Gray, TextAlignment.Right);
        for (var i = 0; i < n; i++)
        {
            var x0 = Snap(left + i * bw);
            var x1 = Snap(left + (i + 1) * bw);
            var isDay = i < hs.IsDay.Length && hs.IsDay[i];
            dc.DrawRectangle(isDay ? Vga.Brown : Vga.Blue, null, new Rect(x0, Snap(RowY(2) + ch * 0.25), x1 - x0, Snap(ch * 0.5)));
            if (i < hs.CloudCover.Length && !double.IsNaN(hs.CloudCover[i]))
                dc.DrawRectangle(Vga.Shade(hs.CloudCover[i] / 100.0), null, new Rect(x0, Snap(RowY(3) + ch * 0.125), x1 - x0, Snap(ch * 0.75)));
        }

        // --- rozdělení výšky mezi panely
        var firstRow = 4;
        var rem = rows - firstRow;

        // Oblačnost (tři křivky) se přidá jen tehdy, když je pro pět panelů dost řádků.
        var withCloud = rem >= 32;
        var iCloud = withCloud ? 1 : -1;
        var iPrecip = withCloud ? 2 : 1;
        var iWind = withCloud ? 3 : 2;
        var iPressure = withCloud ? 4 : 3;
        var panelCount = withCloud ? 5 : 4;

        int[] weights = withCloud ? [3, 2, 2, 3, 2] : [3, 2, 3, 2];
        var heights = weights.Select(wt => rem * wt / weights.Sum()).ToArray();
        heights[0] += rem - heights.Sum();

        var rowStart = firstRow;
        var panelTops = new double[panelCount];
        var plots = new Rect[panelCount];
        for (var p = 0; p < panelCount; p++)
        {
            panelTops[p] = RowY(rowStart);
            var reserve = p == iWind ? 1.5 : 0.5;               // vítr: místo pro řádek šipek
            var plotH = (heights[p] - 1 - reserve) * ch;
            plots[p] = new Rect(left, panelTops[p] + ch, right - left, Math.Max(plotH, ch * 2));
            rowStart += heights[p];
        }
        var lastBottom = plots[panelCount - 1].Bottom;

        // --- panel 1: teplota
        {
            var plot = plots[0];
            var (lo, hi) = WeatherMath.Range(hs.Temperature.Concat(hs.ApparentTemperature));
            var sc = WeatherMath.NiceScale(lo, hi, Math.Max(3, (int)(plot.Height / (ch * 2.2)) + 1));
            DrawPanelBackground(dc, plot, hs, n, X, sc.Min, sc.Max, sc.Step, dark, dotted, axis, zeroLine: true);

            var tx = TextAdvance(dc, "Teplota °C ", left, panelTops[0], Vga.LightRed);
            TextAdvance(dc, "pocitová", tx, panelTops[0], Vga.DarkGray);

            DrawSeries(dc, MakePen(Vga.DarkGray, 1, true), hs.ApparentTemperature, plot, sc.Min, sc.Max, X);
            DrawSeries(dc, MakePen(Vga.LightRed, 2), hs.Temperature, plot, sc.Min, sc.Max, X);

            // denní maxima a minima přímo v grafu
            foreach (var day in hs.Time.Select(t => t.Date).Distinct())
            {
                var idx = Enumerable.Range(0, n).Where(i => hs.Time[i].Date == day && !double.IsNaN(hs.Temperature[i])).ToList();
                if (idx.Count < 6) continue;
                var iMax = idx.MaxBy(i => hs.Temperature[i]);
                var iMin = idx.MinBy(i => hs.Temperature[i]);
                var yMax = MapY(plot, sc.Min, sc.Max, hs.Temperature[iMax]);
                var yMin = MapY(plot, sc.Min, sc.Max, hs.Temperature[iMin]);
                Text(dc, F(hs.Temperature[iMax], "0") + "°", X(iMax), Math.Max(plot.Top - ch * 0.5, yMax - ch - 2), Vga.Yellow, TextAlignment.Center);
                Text(dc, F(hs.Temperature[iMin], "0") + "°", X(iMin), Math.Min(plot.Bottom - ch * 0.5, yMin + 2), Vga.LightCyan, TextAlignment.Center);
            }
        }

        // --- panel: oblačnost – nízká, střední a vysoká zvlášť
        if (withCloud)
        {
            var plot = plots[iCloud];
            DrawPanelBackground(dc, plot, hs, n, X, 0, 100, 50, dark, dotted, axis, zeroLine: false);

            var tx = TextAdvance(dc, "Oblačnost % ", left, panelTops[iCloud], Vga.Gray);
            tx = TextAdvance(dc, "nízká ", tx, panelTops[iCloud], Vga.White);
            tx = TextAdvance(dc, "střední ", tx, panelTops[iCloud], Vga.LightBlue);
            TextAdvance(dc, "vysoká", tx, panelTops[iCloud], Vga.LightCyan);

            var any = hs.CloudLow.Concat(hs.CloudMid).Concat(hs.CloudHigh).Any(v => !double.IsNaN(v));
            if (any)
            {
                DrawSeries(dc, MakePen(Vga.LightCyan), hs.CloudHigh, plot, 0, 100, X);
                DrawSeries(dc, MakePen(Vga.LightBlue), hs.CloudMid, plot, 0, 100, X);
                DrawSeries(dc, MakePen(Vga.White, 2), hs.CloudLow, plot, 0, 100, X);
            }
            else
            {
                Text(dc, "bez dat (doplní se při příštím stažení)", plot.Left + plot.Width / 2, plot.Top + plot.Height / 2 - ch / 2, Vga.DarkGray, TextAlignment.Center);
            }
        }

        // --- panel 2: srážky
        {
            var plot = plots[iPrecip];
            var (_, pmaxRaw) = WeatherMath.Range(hs.Precipitation);
            var sc = WeatherMath.NiceScale(0, Math.Max(1.0, double.IsNaN(pmaxRaw) ? 1.0 : pmaxRaw), Math.Max(2, (int)(plot.Height / (ch * 2.2)) + 1));
            DrawPanelBackground(dc, plot, hs, n, X, sc.Min, sc.Max, sc.Step, dark, dotted, axis, zeroLine: false);
            var tx = TextAdvance(dc, "Srážky mm/h ", left, panelTops[iPrecip], Vga.LightCyan);
            TextAdvance(dc, "sníh", tx, panelTops[iPrecip], Vga.White);

            for (var i = 0; i < n; i++)
            {
                var p = hs.Precipitation[i];
                if (double.IsNaN(p) || p <= 0) continue;
                var y = MapY(plot, sc.Min, sc.Max, Math.Min(p, sc.Max));
                var x0 = Snap(left + i * bw) + 1 / _ppd;
                var x1 = Snap(left + (i + 1) * bw) - 1 / _ppd;
                var top = Math.Min(y, plot.Bottom - 1 / _ppd);
                var snow = i < hs.Snowfall.Length && !double.IsNaN(hs.Snowfall[i]) && hs.Snowfall[i] > 0;
                dc.DrawRectangle(snow ? Vga.White : Vga.LightCyan, null,
                    new Rect(x0, top, Math.Max(1 / _ppd, x1 - x0), Math.Max(1 / _ppd, plot.Bottom - top)));
            }
        }

        // --- panel 3: vítr
        {
            var plot = plots[iWind];
            var (_, gmaxRaw) = WeatherMath.Range(hs.WindGusts.Concat(hs.WindSpeed));
            var sc = WeatherMath.NiceScale(0, Math.Max(10, double.IsNaN(gmaxRaw) ? 10 : gmaxRaw), Math.Max(3, (int)(plot.Height / (ch * 2.2)) + 1));
            DrawPanelBackground(dc, plot, hs, n, X, sc.Min, sc.Max, sc.Step, dark, dotted, axis, zeroLine: false);

            var tx = TextAdvance(dc, "Vítr m/s ", left, panelTops[iWind], Vga.LightGreen);
            tx = TextAdvance(dc, "rychlost ", tx, panelTops[iWind], Vga.LightGreen);
            tx = TextAdvance(dc, "nárazy ", tx, panelTops[iWind], Vga.Yellow);
            TextAdvance(dc, "šipka = kam vane", tx, panelTops[iWind], Vga.Gray);

            DrawSeries(dc, MakePen(Vga.Yellow), hs.WindGusts, plot, sc.Min, sc.Max, X);
            DrawSeries(dc, MakePen(Vga.LightGreen, 2), hs.WindSpeed, plot, sc.Min, sc.Max, X);

            var arrowY = plot.Bottom + ch * 0.9;
            var pen0 = MakePen(Vga.Gray);
            for (var i = 0; i < n; i++)
            {
                if (hs.Time[i].Hour % 3 != 0) continue;
                if (double.IsNaN(hs.WindDirection[i]) || double.IsNaN(hs.WindSpeed[i])) continue;
                var pen = MakePen(WindBrush(hs.WindSpeed[i]));
                DrawArrow(dc, pen, new Point(Mid(X(i)), Mid(arrowY)), hs.WindDirection[i], ch * 0.85);
            }
            _ = pen0;
        }

        // --- panel 4: tlak
        {
            var plot = plots[iPressure];
            var (lo, hi) = WeatherMath.Range(hs.Pressure);
            if (!double.IsNaN(lo) && hi - lo < 6) { var m = (lo + hi) / 2; lo = m - 3; hi = m + 3; }
            var sc = WeatherMath.NiceScale(lo, hi, Math.Max(2, (int)(plot.Height / (ch * 2.2)) + 1));
            DrawPanelBackground(dc, plot, hs, n, X, sc.Min, sc.Max, sc.Step, dark, dotted, axis, zeroLine: false);
            TextAdvance(dc, "Tlak hPa (hladina moře)", left, panelTops[iPressure], Vga.LightMagenta);
            DrawSeries(dc, MakePen(Vga.LightMagenta, 2), hs.Pressure, plot, sc.Min, sc.Max, X);
        }

        // --- svislá čára "teď"
        var nowIdx = (DateTime.Now - hs.Time[0]).TotalHours;
        if (nowIdx >= -0.5 && nowIdx <= n - 0.5)
        {
            var x = Mid(X(nowIdx));
            dc.DrawLine(MakePen(Vga.LightCyan), new Point(x, Snap(RowY(2))), new Point(x, lastBottom));
        }
    }

    private void DrawPanelBackground(DrawingContext dc, Rect plot, HourlySeries hs, int n, Func<double, double> X,
        double min, double max, double step, Pen dark, Pen dotted, Pen axis, bool zeroLine)
    {
        double cw = CW, ch = CH;

        // svislá mřížka po 6 hodinách, půlnoc plnou čarou
        for (var i = 0; i < n; i++)
        {
            var hr = hs.Time[i].Hour;
            if (hr % 6 != 0) continue;
            var x = Mid(X(i));
            dc.DrawLine(hr == 0 ? dark : dotted, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        // vodorovná mřížka s popisky osy
        var fmt = step < 1 ? "0.0" : "0";
        var zeroPen = MakePen(Vga.LightBlue);
        for (var v = min; v <= max + step * 0.001; v += step)
        {
            var y = MapY(plot, min, max, v);
            var isZero = zeroLine && min < 0 && max > 0 && Math.Abs(v) < step * 0.001;
            dc.DrawLine(isZero ? zeroPen : dotted, new Point(plot.Left, y), new Point(plot.Right, y));
            Text(dc, v.ToString(fmt, Cs), plot.Left - cw * 0.5, y - ch / 2, Vga.Gray, TextAlignment.Right);
        }

        dc.DrawLine(axis, new Point(Mid(plot.Left), plot.Top), new Point(Mid(plot.Left), plot.Bottom));
        dc.DrawLine(axis, new Point(plot.Left, Mid(plot.Bottom)), new Point(plot.Right, Mid(plot.Bottom)));
    }

    private void DrawSeries(DrawingContext dc, Pen pen, double[] values, Rect plot, double min, double max, Func<double, double> X)
    {
        var geometry = new StreamGeometry();
        var segments = 0;
        using (var ctx = geometry.Open())
        {
            var open = false;
            for (var i = 0; i < values.Length; i++)
            {
                if (double.IsNaN(values[i])) { open = false; continue; }
                var p = new Point(Mid(X(i)), MapY(plot, min, max, Math.Clamp(values[i], min, max)));
                if (!open) { ctx.BeginFigure(p, false, false); open = true; segments++; }
                else ctx.LineTo(p, true, false);
            }
        }
        if (segments == 0) return;
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Šipka míří tam, kam vítr vane (meteorologický směr udává, ODKUD vane, proto +180°).</summary>
    private static void DrawArrow(DrawingContext dc, Pen pen, Point c, double fromDegrees, double len)
    {
        var phi = (fromDegrees + 180.0) * Math.PI / 180.0;
        double dx = Math.Sin(phi), dy = -Math.Cos(phi);
        var tip = new Point(c.X + dx * len / 2, c.Y + dy * len / 2);
        var tail = new Point(c.X - dx * len / 2, c.Y - dy * len / 2);
        dc.DrawLine(pen, tail, tip);
        var back = Math.Atan2(-dy, -dx);
        foreach (var side in new[] { -0.5, 0.5 })
        {
            var a = back + side;
            dc.DrawLine(pen, tip, new Point(tip.X + Math.Cos(a) * len * 0.38, tip.Y + Math.Sin(a) * len * 0.38));
        }
    }

    // ------------------------------------------------------------------ "nyní" a denní přehled

    private void DrawInfo(DrawingContext dc, Rect r, ForecastResult f, bool landscape)
    {
        double cw = CW, ch = CH;
        var hs = f.Hourly;
        var idx = WeatherMath.NearestIndex(hs.Time, DateTime.Now);

        double nowX = r.Left;
        double dailyX = landscape ? r.Left : r.Left + Math.Floor(r.Width / cw * 0.42) * cw;
        double dailyY = landscape ? r.Top + 9.5 * ch : r.Top;

        if (idx >= 0)
        {
            var y = r.Top;
            Text(dc, "NYNÍ " + hs.Time[idx].ToString("HH:mm", Cs), nowX, y, Vga.White);
            y += ch;
            const double vx = 11;

            void Row(string label, string value, Brush brush)
            {
                Text(dc, label, nowX, y, Vga.Gray);
                Text(dc, value, nowX + vx * cw, y, brush);
                y += ch;
            }

            var ws = hs.WindSpeed[idx];
            Row("Teplota", $"{F(hs.Temperature[idx], "0.0")} °C  (pocit {F(hs.ApparentTemperature[idx], "0.0")})", Vga.LightRed);
            Row("Vítr", $"{F(ws, "0.0")} m/s  {WeatherMath.CompassName(hs.WindDirection[idx])}  {WeatherMath.Beaufort(ws)} Bft", WindBrush(ws));
            Row("Nárazy", $"{F(hs.WindGusts[idx], "0.0")} m/s", WindBrush(hs.WindGusts[idx]));
            Row("Srážky", $"{F(hs.Precipitation[idx], "0.0")} mm/h", Vga.LightCyan);
            Row("Oblačnost", $"{F(hs.CloudCover[idx], "0")} %", Vga.Gray);
            Row("Vlhkost", $"{F(hs.Humidity[idx], "0")} %", Vga.Gray);
            Row("Tlak", $"{F(hs.Pressure[idx], "0")} hPa", Vga.LightMagenta);
        }

        DrawDaily(dc, dailyX, dailyY, f);
    }

    private void DrawDaily(DrawingContext dc, double x, double y, ForecastResult f)
    {
        double cw = CW, ch = CH;
        var d = f.Daily;
        var hs = f.Hourly;
        if (d.Count == 0) return;

        const int labelCols = 13, dayCols = 10;
        var days = Math.Min(3, d.Count);

        Text(dc, "PŘEHLED", x, y, Vga.White);
        for (var k = 0; k < days; k++)
            Text(dc, d.Date[k].ToString("ddd d.M.", Cs), x + (labelCols + (k + 1) * dayCols) * cw, y, Vga.Yellow, TextAlignment.Right);
        y += ch;

        void Row(string label, Func<int, (string Text, Brush Brush)> cell)
        {
            Text(dc, label, x, y, Vga.Gray);
            for (var k = 0; k < days; k++)
            {
                var (t, b) = cell(k);
                Text(dc, t, x + (labelCols + (k + 1) * dayCols) * cw, y, b, TextAlignment.Right);
            }
            y += ch;
        }

        Row("Tmax °C", k => (F(d.TempMax[k], "0"), Vga.LightRed));
        Row("Tmin °C", k => (F(d.TempMin[k], "0"), Vga.LightCyan));
        Row("Srážky mm", k => (F(d.PrecipSum[k], "0.0"), d.PrecipSum[k] > 0 ? Vga.LightCyan : Vga.Gray));
        Row("Vítr m/s", k =>
        {
            var idx = Enumerable.Range(0, hs.Count)
                .Where(i => hs.Time[i].Date == d.Date[k].Date && !double.IsNaN(hs.WindSpeed[i]))
                .OrderByDescending(i => hs.WindSpeed[i])
                .Cast<int?>().FirstOrDefault();
            var dir = idx is int ii ? " " + WeatherMath.CompassName(hs.WindDirection[ii]) : "";
            return (F(d.WindMax[k], "0") + dir, WindBrush(d.WindMax[k]));
        });
        Row("Nárazy m/s", k => (F(d.GustMax[k], "0"), WindBrush(d.GustMax[k])));
        Row("Východ", k => (d.Sunrise[k]?.ToString("HH:mm", Cs) ?? "-", Vga.Brown));
        Row("Západ", k => (d.Sunset[k]?.ToString("HH:mm", Cs) ?? "-", Vga.Brown));
    }

    // ------------------------------------------------------------------ spodní část: radar + Flymet

    // Zobrazený výřez radaru = oblast, kam až sahají data (zbytek PNG je prázdný).
    private const double RadarViewLonMax = 19.624, RadarViewLatMax = 51.458;

    private static (double U0, double V0, double W, double H) RadarView()
    {
        var (u0, v0) = RadarGeo.ToUnit(RadarViewLatMax, RadarGeo.LonMin);
        var (u1, v1) = RadarGeo.ToUnit(RadarGeo.LatMin, RadarViewLonMax);
        return (u0, v0, u1 - u0, v1 - v0);
    }

    /// <summary>Poměr šířky k výšce zobrazeného výřezu radaru.</summary>
    private static double RadarAspect
    {
        get
        {
            var v = RadarView();
            return v.W * RadarGeo.Aspect / v.H;
        }
    }

    private void DrawLower(DrawingContext dc, Rect r, DashboardState st, bool landscape)
    {
        _radarVisible = false;
        var radarOn = _settings.ShowRadar;
        var flyOn = _settings.ShowFlymet;
        if (r.Width < 100 || r.Height < CH * 4) return;
        if (!radarOn && !flyOn) return;
        if (!radarOn) { DrawFlymet(dc, r, st.Flymet); return; }
        if (!flyOn) { DrawRadar(dc, r, st); return; }

        double cw = CW, ch = CH;
        if (landscape)
        {
            // radar nahoře přes celou šířku, Flymet pod ním, je-li místo; jinak se střídají
            var need = r.Width / RadarAspect + 2 * ch;
            if (r.Height >= need + 8 * ch)
            {
                DrawRadar(dc, new Rect(r.Left, r.Top, r.Width, need), st);
                var fy = r.Top + need + ch;
                DrawFlymet(dc, new Rect(r.Left, fy, r.Width, Math.Max(0, r.Bottom - fy)), st.Flymet);
            }
            else
            {
                // 30 s radar, 15 s Flymet (chybí-li jedno z nich, zobrazí se jen druhé)
                var slot = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond) % 45;
                var showRadar = _radar.Count > 0 && (slot < 30 || st.Flymet is null);
                if (showRadar) DrawRadar(dc, r, st);
                else DrawFlymet(dc, r, st.Flymet);
            }
        }
        else
        {
            // na výšku vedle sebe: vlevo radar, vpravo Flymet
            var rw = Math.Floor(r.Width * 0.5 / cw) * cw;
            DrawRadar(dc, new Rect(r.Left, r.Top, rw, r.Height), st);
            DrawFlymet(dc, new Rect(r.Left + rw + cw, r.Top, Math.Max(0, r.Width - rw - cw), r.Height), st.Flymet);
        }
    }

    private void DrawRadar(DrawingContext dc, Rect r, DashboardState st)
    {
        double cw = CW, ch = CH;
        if (r.Width < 24 * cw || r.Height < 8 * ch) return;

        var frames = _radar;
        Text(dc, "RADAR ČHMÚ", r.Left, r.Top, Vga.White);
        Text(dc, "max. odrazivost", r.Right, r.Top, Vga.DarkGray, TextAlignment.Right);

        if (frames.Count == 0)
        {
            Text(dc, "Čekám na radarové snímky…", r.Left, r.Top + ch, Vga.DarkGray);
            return;
        }
        _radarVisible = true;
        if (_radarIndex >= frames.Count) _radarIndex = 0;
        var frame = frames[_radarIndex];

        // --- mapa se zachovaným poměrem stran
        var area = new Rect(r.Left, r.Top + ch, r.Width, r.Height - 2 * ch);
        var aspect = RadarAspect;
        var mh = Math.Min(area.Height, area.Width / aspect);
        var mw = mh * aspect;
        var map = new Rect(Snap(area.Left), Snap(area.Top), Snap(mw), Snap(mh));

        var view = RadarView();
        var fullW = map.Width / view.W;
        var fullH = map.Height / view.H;
        var img = new Rect(map.Left - view.U0 * fullW, map.Top - view.V0 * fullH, fullW, fullH);

        Point Project(double lat, double lon)
        {
            var (ux, uy) = RadarGeo.ToUnit(lat, lon);
            return new Point(img.Left + ux * img.Width, img.Top + uy * img.Height);
        }

        _map ??= MapData.Load();
        if (_mapCache is null || _mapCache.Value.Rect != img)
            _mapCache = (img, BuildLines(_map.Borders, Project), BuildLines(_map.Regions, Project));
        var cache = _mapCache.Value;

        dc.DrawRectangle(Vga.Black, MakePen(Vga.DarkGray), new Rect(map.Left - 1 / _ppd, map.Top - 1 / _ppd, map.Width + 2 / _ppd, map.Height + 2 / _ppd));
        dc.PushClip(new RectangleGeometry(map));

        dc.DrawGeometry(null, MakePen(Vga.DarkGray, 1, true), cache.Regions);
        dc.DrawImage(frame.Image, img);
        dc.DrawGeometry(null, MakePen(Vga.Gray), cache.Borders);

        // orientační města (názvy jen na větší mapě, aby se nepřekrývaly)
        var names = map.Width >= 520;
        foreach (var c in RadarGeo.Cities)
        {
            if (!RadarGeo.IsInside(c.Lat, c.Lon)) continue;
            var pt = Project(c.Lat, c.Lon);
            if (!map.Contains(pt)) continue;
            dc.DrawRectangle(Vga.DarkGray, null, new Rect(Snap(pt.X) - 1 / _ppd, Snap(pt.Y) - 1 / _ppd, 3 / _ppd, 3 / _ppd));
            if (names) Text(dc, c.Name, pt.X + 4 / _ppd, pt.Y - ch / 2, Vga.DarkGray);
        }

        // právě zobrazené místo: křížek a kód
        var here = Project(st.Place.Latitude, st.Place.Longitude);
        if (map.Contains(here))
        {
            var pen = MakePen(Vga.Yellow);
            var a = 6 / _ppd;
            dc.DrawLine(pen, new Point(Mid(here.X) - a, Mid(here.Y)), new Point(Mid(here.X) + a, Mid(here.Y)));
            dc.DrawLine(pen, new Point(Mid(here.X), Mid(here.Y) - a), new Point(Mid(here.X), Mid(here.Y) + a));
            var label = st.Place.Code.Length > 0 ? st.Place.Code : st.Place.Name;
            var lx = here.X + 8 / _ppd;
            if (lx + label.Length * cw > map.Right) lx = here.X - 8 / _ppd - label.Length * cw;
            Text(dc, label, lx, Math.Clamp(here.Y - ch / 2, map.Top, map.Bottom - ch), Vga.Yellow);
        }
        dc.Pop();

        // --- čas snímku a ukazatel průběhu
        var local = frame.TimeUtc.ToLocalTime();
        var caption = frame.IsForecast
            ? $"{local:HH:mm}  extrapolace +{frame.LeadMinutes} min"
            : $"{local:HH:mm}  měření";
        var y = map.Bottom + 2 / _ppd;
        Text(dc, caption, map.Left, y, frame.IsForecast ? Vga.LightCyan : Vga.Yellow);

        var count = frames.Count;
        var barW = Math.Min(map.Width * 0.45, count * 8 / _ppd);
        var tickW = barW / count;
        var bx = map.Right - barW;
        for (var i = 0; i < count; i++)
        {
            var brush = i == _radarIndex ? Vga.White : frames[i].IsForecast ? Vga.Cyan : Vga.DarkGray;
            dc.DrawRectangle(brush, null, new Rect(Snap(bx + i * tickW), Snap(y + 3 / _ppd), Math.Max(1 / _ppd, Snap(tickW) - 2 / _ppd), Snap(ch * 0.6)));
        }
    }

    private StreamGeometry BuildLines(IReadOnlyList<GeoPoint[]> lines, Func<double, double, Point> project)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            foreach (var line in lines)
            {
                for (var i = 0; i < line.Length; i++)
                {
                    var p = project(line[i].Lat, line[i].Lon);
                    if (i == 0) ctx.BeginFigure(p, false, false);
                    else ctx.LineTo(p, true, false);
                }
            }
        }
        g.Freeze();
        return g;
    }

    // ------------------------------------------------------------------ Flymet

    private void DrawFlymet(DrawingContext dc, Rect r, System.Windows.Media.Imaging.BitmapSource? image)
    {
        double ch = CH;
        if (r.Width < 100 || r.Height < ch * 4) return;

        if (image is null)
        {
            Text(dc, "Flymet: pro toto místo není obrázek k dispozici", r.Left, r.Top, Vga.DarkGray);
            return;
        }

        Text(dc, "FLYMET (Meteopress)", r.Left, r.Top, Vga.White);
        var area = new Rect(r.Left, r.Top + ch, r.Width, r.Height - ch);

        // Obrázek dostává rozměry v DIP; zdrojové pixely přepočteme tak, aby 1 px obrázku = 1 fyzický pixel.
        var iw = image.PixelWidth / _ppd;
        var ih = image.PixelHeight / _ppd;
        var k = Math.Min(area.Width / iw, area.Height / ih);
        k = Math.Min(k, 2.0);
        if (k <= 0) return;
        var w = iw * k;
        var h = ih * k;
        dc.DrawImage(image, new Rect(Snap(area.Left), Snap(area.Top), Snap(w), Snap(h)));
    }
}
