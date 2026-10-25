using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace Meteo.Windows;

/// <summary>
/// Ikona vedle hodin: ukazuje, že Meteo opravdu běží, a slouží k diagnostice (informace, složka s logem, ruční přepnutí místa).
/// Žádné nastavení v ní není, to se mění v kódu (AppSettings).
/// </summary>
internal sealed class AppTray : IDisposable
{
    private readonly AppServices _s;
    private readonly DashboardHost _host;
    private readonly string _modeName;
    private readonly string _exitHint;
    private readonly TrayIcon _icon;

    /// <summary>Průzkumník se restartoval a ikona se vrátila (živé pozadí si musí okna zavěsit znovu).</summary>
    public event Action? ShellRestarted;

    /// <param name="modeName">Např. "Samostatné okno" nebo "Živé pozadí".</param>
    /// <param name="exitHint">Jak se program ukončí, např. "Esc" nebo "Win+D".</param>
    /// <param name="desktopMode">Živé pozadí: nabídka ukazuje klávesové zkratky a volbu "Spouštět s Windows".</param>
    public AppTray(AppServices services, DashboardHost host, string modeName, string exitHint, bool desktopMode)
    {
        _s = services;
        _host = host;
        _modeName = modeName;
        _exitHint = exitHint;
        Func<bool>? autoStart = null;
        if (desktopMode) autoStart = AutoStart.IsEnabled;
        _icon = new TrayIcon($"Meteo – {modeName.ToLowerInvariant()}, konec: {exitHint}")
        {
            ShowKeyHints = desktopMode,
            IsAutoStartEnabled = autoStart,
        };
        _icon.Command += cmd => Application.Current.Dispatcher.InvokeAsync(() => OnCommand(cmd));
        _icon.ShellRestarted += () => ShellRestarted?.Invoke();
    }

    private void OnCommand(int cmd)
    {
        switch (cmd)
        {
            case TrayIcon.CmdNext: _host.Next(); break;
            case TrayIcon.CmdPrevious: _host.Previous(); break;
            case TrayIcon.CmdRefresh: _host.Refresh(); break;
            case TrayIcon.CmdInfo: ShowInfo(); break;
            case TrayIcon.CmdOpenFolder:
                try { Process.Start(new ProcessStartInfo(_s.DataDir) { UseShellExecute = true }); }
                catch (Exception ex) { _s.Log(ex); }
                break;
            case TrayIcon.CmdAutoStart:
                if (!AutoStart.Set(!AutoStart.IsEnabled()))
                    System.Windows.MessageBox.Show("Nepodařilo se zapsat do registru.", "Meteo",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                break;
            case TrayIcon.CmdExit:
                var answer = System.Windows.MessageBox.Show(
                    "Ukončit Meteo?", "Meteo", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer == MessageBoxResult.Yes) Application.Current.Shutdown();
                break;
        }
    }

    private void ShowInfo()
    {
        var exe = Environment.ProcessPath;
        var built = exe is null ? "?" : File.GetLastWriteTime(exe).ToString("d.M.yyyy HH:mm");
        var st = _host.Current;
        var text =
            "Meteo – zobrazení předpovědi ČHMÚ ALADIN a srážkového radaru.\n\n" +
            $"Režim: {_modeName}\n" +
            $"Ukončení: {_exitHint}\n" +
            $"Místo: {st?.Place.Title ?? "–"}\n" +
            $"Stav: {(string.IsNullOrEmpty(st?.Status) ? "v pořádku" : st.Status)}\n" +
            $"Sestaveno: {built}\n" +
            $"Verze: {Assembly.GetExecutingAssembly().GetName().Version}\n" +
            $"Data a log: {_s.DataDir}";
        System.Windows.MessageBox.Show(text, "Meteo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Dispose() => _icon.Dispose();
}
