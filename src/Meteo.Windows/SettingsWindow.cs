using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Meteo.Core;

namespace Meteo.Windows;

/// <summary>Nastavení (/c): seznam míst ke střídání (ICAO kódy, obce, souřadnice), interval, měřítko, font.</summary>
internal sealed class SettingsWindow : Window
{
    private readonly AppServices _s;
    private readonly ListBox _list = new() { Height = 170, DisplayMemberPath = nameof(Place.Title) };
    private readonly TextBox _search = new() { MinWidth = 220 };
    private readonly ComboBox _results = new() { MinWidth = 330, DisplayMemberPath = nameof(GeocodingClient.Hit.Display) };
    private readonly TextBox _rotate = new() { Width = 70 };
    private readonly TextBox _refresh = new() { Width = 70 };
    private readonly TextBox _scale = new() { Width = 70 };
    private readonly TextBox _font = new() { Width = 220 };
    private readonly CheckBox _flymet = new() { Content = "Zobrazit meteogram Flymet (pokud pro místo existuje)" };
    private readonly CheckBox _radar = new() { Content = "Zobrazit srážkový radar ČHMÚ (animace)" };
    private readonly CheckBox _radarFc = new() { Content = "K radaru přidat extrapolaci na dalších 60 minut" };
    private readonly TextBox _radarHours = new() { Width = 70 };
    private readonly CheckBox _allMonitors = new() { Content = "Stejný obsah na všech monitorech (jinak jsou ostatní černé)" };
    private readonly CheckBox _ascii = new() { Content = "Pouze ASCII (náhrada diakritiky, pokud ji font nemá)" };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };

    public SettingsWindow(AppServices services)
    {
        _s = services;
        Title = "Meteo – nastavení";
        Width = 600;
        Height = 820;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        foreach (var p in _s.Catalog.Selected()) _list.Items.Add(p);

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock { Text = "Místa ve střídání (shora dolů):", FontWeight = FontWeights.SemiBold });

        var listRow = new DockPanel { Margin = new Thickness(0, 4, 0, 8), LastChildFill = true };
        var side = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        side.Children.Add(MakeButton("Nahoru", (_, _) => Move(-1)));
        side.Children.Add(MakeButton("Dolů", (_, _) => Move(1)));
        side.Children.Add(MakeButton("Odebrat", (_, _) => Remove()));
        DockPanel.SetDock(side, Dock.Right);
        listRow.Children.Add(side);
        listRow.Children.Add(_list);
        root.Children.Add(listRow);

        root.Children.Add(new TextBlock
        {
            Text = "Přidat místo – ICAO kód (LKPR), název letiště nebo obce, nebo souřadnice (49.58, 15.76):",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        var find = MakeButton("Hledat", OnSearch);
        searchRow.Children.Add(_search);
        searchRow.Children.Add(find);
        root.Children.Add(searchRow);
        _search.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OnSearch(this, new RoutedEventArgs()); };

        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        addRow.Children.Add(_results);
        addRow.Children.Add(MakeButton("Přidat vybrané", OnAdd));
        root.Children.Add(addRow);
        root.Children.Add(_status);

        _rotate.Text = _s.Settings.RotateSeconds.ToString(CultureInfo.InvariantCulture);
        _refresh.Text = _s.Settings.RefreshMinutes.ToString(CultureInfo.InvariantCulture);
        _scale.Text = _s.Settings.UiScale.ToString(CultureInfo.InvariantCulture);
        _font.Text = _s.Settings.FontFamily;
        _flymet.IsChecked = _s.Settings.ShowFlymet;
        _allMonitors.IsChecked = _s.Settings.AllMonitors;
        _radar.IsChecked = _s.Settings.ShowRadar;
        _radarFc.IsChecked = _s.Settings.RadarForecast;
        _radarHours.Text = _s.Settings.RadarHours.ToString(CultureInfo.InvariantCulture);
        _ascii.IsChecked = _s.Settings.AsciiOnly;

        root.Children.Add(Row("Přepnutí na další místo po (s):", _rotate));
        root.Children.Add(Row("Obnovení předpovědi po (min):", _refresh));
        root.Children.Add(Row("Zvětšení písma 8×16 (0 = auto):", _scale));
        root.Children.Add(Row("Font:", _font));
        root.Children.Add(Row("Radar: hodin zpět (1–6):", _radarHours));
        foreach (var cb in new[] { _radar, _radarFc, _flymet, _allMonitors, _ascii }) { cb.Margin = new Thickness(0, 4, 0, 0); root.Children.Add(cb); }

        root.Children.Add(new TextBlock
        {
            Text = "Data a nastavení: " + _s.DataDir,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "Uložit", IsDefault = false, Padding = new Thickness(18, 4, 18, 4), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Zrušit", IsCancel = true, Padding = new Thickness(18, 4, 18, 4) };
        ok.Click += OnSave;
        cancel.Click += (_, _) => Close();
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static Button MakeButton(string text, RoutedEventHandler click)
    {
        var b = new Button { Content = text, Margin = new Thickness(6, 0, 0, 4), Padding = new Thickness(10, 2, 10, 2), MinWidth = 80 };
        b.Click += click;
        return b;
    }

    private static StackPanel Row(string label, UIElement input)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        p.Children.Add(new TextBlock { Text = label, Width = 230, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(input);
        return p;
    }

    private void Move(int delta)
    {
        var i = _list.SelectedIndex;
        var j = i + delta;
        if (i < 0 || j < 0 || j >= _list.Items.Count) return;
        var item = _list.Items[i];
        _list.Items.RemoveAt(i);
        _list.Items.Insert(j, item);
        _list.SelectedIndex = j;
    }

    private void Remove()
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        _list.Items.RemoveAt(i);
        if (_list.Items.Count > 0) _list.SelectedIndex = Math.Min(i, _list.Items.Count - 1);
    }

    private async void OnSearch(object sender, RoutedEventArgs e)
    {
        var q = _search.Text.Trim();
        if (q.Length < 2) return;

        // 1) co lze rozpoznat hned a bez internetu: ICAO kód, uložené místo, souřadnice, letiště
        var hits = new List<GeocodingClient.Hit>();
        if (_s.Catalog.Find(q) is { } exact) hits.Add(ToHit(exact));
        foreach (var p in _s.Catalog.SearchAirports(q))
            if (!hits.Any(h => h.Place.Key == p.Key && h.Place.Latitude == p.Latitude)) hits.Add(ToHit(p));

        _results.ItemsSource = hits.ToList();
        _results.SelectedIndex = hits.Count > 0 ? 0 : -1;
        _status.Text = "Hledám obce…";

        // 2) obce přes internet (selže-li, zůstanou letiště)
        try
        {
            var towns = await _s.Geocoding.SearchAsync(q, CancellationToken.None);
            hits.AddRange(towns);
            var keep = _results.SelectedIndex;
            _results.ItemsSource = hits.ToList();
            _results.SelectedIndex = hits.Count > 0 ? Math.Max(0, keep) : -1;
            _status.Text = hits.Count == 0
                ? "Nic nenalezeno."
                : $"Nalezeno: {hits.Count}. Vyberte v rozbalovacím seznamu a stiskněte „Přidat vybrané“.";
        }
        catch (Exception ex)
        {
            _status.Text = hits.Count == 0
                ? "Obce se nepodařilo vyhledat (" + ex.Message + ")."
                : $"Nalezeno letišť: {hits.Count}. Obce se nepodařilo vyhledat (offline?).";
        }
    }

    private static GeocodingClient.Hit ToHit(Place p)
    {
        var display = p.Title;
        if (p.Code.Length > 0 && p.Locality.Length > 0 && !p.Name.Contains(p.Locality, StringComparison.OrdinalIgnoreCase))
            display += " – " + p.Locality;
        return new GeocodingClient.Hit(p, display);
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (_results.SelectedItem is not GeocodingClient.Hit hit) return;
        var p = hit.Place;
        var exists = _list.Items.OfType<Place>().Any(x => x.Key == p.Key && Math.Abs(x.Latitude - p.Latitude) < 0.001);
        if (exists) { _status.Text = $"{p.Title} už v seznamu je."; return; }
        _list.Items.Add(p);
        _list.SelectedItem = p;
        _status.Text = "Přidáno: " + hit.Display;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var st = _s.Settings;
        var chosen = _list.Items.OfType<Place>().ToList();
        if (chosen.Count == 0) { _status.Text = "Přidejte alespoň jedno místo."; return; }

        st.Places = chosen.Select(p => p.Key).ToList();
        // Letiště se dohledají podle kódu z vestavěné databáze; ostatní místa (obce) si uložíme i se souřadnicemi.
        st.CustomPlaces = chosen.Where(p => !_s.Catalog.IsAirport(p)).ToList();

        st.RotateSeconds = Parse(_rotate.Text, st.RotateSeconds, 10, 3600);
        st.RefreshMinutes = Parse(_refresh.Text, st.RefreshMinutes, 5, 720);
        st.UiScale = Parse(_scale.Text, st.UiScale, 0, 8);
        st.FontFamily = string.IsNullOrWhiteSpace(_font.Text) ? "PxPlus IBM VGA8" : _font.Text.Trim();
        st.ShowFlymet = _flymet.IsChecked == true;
        st.ShowRadar = _radar.IsChecked == true;
        st.RadarForecast = _radarFc.IsChecked == true;
        st.RadarHours = Parse(_radarHours.Text, st.RadarHours, 1, 6);
        st.AllMonitors = _allMonitors.IsChecked == true;
        st.AsciiOnly = _ascii.IsChecked == true;

        try { _s.Store.Save(st); }
        catch (Exception ex) { _status.Text = "Uložení selhalo: " + ex.Message; return; }
        Close();
    }

    private static int Parse(string text, int fallback, int min, int max) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? Math.Clamp(v, min, max) : fallback;
}
