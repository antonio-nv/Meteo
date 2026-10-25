using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Meteo.Windows;

/// <summary>
/// Ikona vedle hodin s nabídkou. Živé pozadí nemá okno, na které by šlo kliknout (plocha ho překrývá),
/// proto jsou ovládací prvky tady. Ikona je v oznamovací oblasti, ne na seznamu spuštěných programů.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    public const int CmdNext = 1, CmdPrevious = 2, CmdRefresh = 3, CmdInfo = 4, CmdOpenFolder = 5, CmdAutoStart = 6, CmdExit = 7;

    private const int WM_APP_TRAY = 0x8001;
    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;
    private readonly string _tip;
    private bool _added;

    /// <summary>Zvolený příkaz z nabídky (CmdXxx).</summary>
    public event Action<int>? Command;

    /// <summary>Průzkumník se restartoval a ikona se vrátila (okna plochy se musí zavěsit znovu).</summary>
    public event Action? ShellRestarted;

    /// <summary>Zeptá se, zda je zapnuté spouštění s Windows (kvůli zatržítku v nabídce). Null = položka se nezobrazí.</summary>
    public Func<bool>? IsAutoStartEnabled { get; set; }

    /// <summary>U položek "Další / Předchozí místo" ukázat klávesovou zkratku (jen živé pozadí, tam okno klávesy nedostává).</summary>
    public bool ShowKeyHints { get; set; }

    public TrayIcon(string tip)
    {
        _tip = tip;
        _taskbarCreated = DesktopNative.RegisterWindowMessage("TaskbarCreated");
        var p = new HwndSourceParameters("MeteoTray", 0, 0)
        {
            ParentWindow = DesktopNative.HWND_MESSAGE,
            WindowStyle = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
        Add();
    }

    private void Add()
    {
        var data = Data();
        data.uFlags = DesktopNative.NIF_MESSAGE | DesktopNative.NIF_ICON | DesktopNative.NIF_TIP;
        data.uCallbackMessage = WM_APP_TRAY;
        data.hIcon = DesktopNative.LoadIcon(IntPtr.Zero, new IntPtr(DesktopNative.IDI_APPLICATION));
        data.szTip = _tip.Length > 120 ? _tip[..120] : _tip;
        _added = DesktopNative.Shell_NotifyIcon(DesktopNative.NIM_ADD, ref data);
    }

    private DesktopNative.NotifyIconData Data() => new()
    {
        cbSize = Marshal.SizeOf<DesktopNative.NotifyIconData>(),
        hWnd = _source.Handle,
        uID = 1,
        szTip = "",
        szInfo = "",
        szInfoTitle = "",
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_APP_TRAY)
        {
            var mouse = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouse is DesktopNative.WM_LBUTTONUP or DesktopNative.WM_RBUTTONUP)
                ShowMenu();
            handled = true;
        }
        else if (_taskbarCreated != 0 && (uint)msg == _taskbarCreated)
        {
            Add();
            ShellRestarted?.Invoke();
        }
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        var menu = DesktopNative.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdNext), ShowKeyHints ? "Další místo\tCtrl+Alt+→" : "Další místo");
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdPrevious), ShowKeyHints ? "Předchozí místo\tCtrl+Alt+←" : "Předchozí místo");
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdRefresh), "Obnovit data");
            DesktopNative.AppendMenu(menu, DesktopNative.MF_SEPARATOR, UIntPtr.Zero, null);
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdInfo), "Informace o Meteo…");
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdOpenFolder), "Otevřít složku s daty a logem");
            if (IsAutoStartEnabled is { } autoStart)
            {
                var auto = DesktopNative.MF_STRING | (autoStart() ? DesktopNative.MF_CHECKED : 0);
                DesktopNative.AppendMenu(menu, auto, new UIntPtr(CmdAutoStart), "Spouštět s Windows");
            }
            DesktopNative.AppendMenu(menu, DesktopNative.MF_SEPARATOR, UIntPtr.Zero, null);
            DesktopNative.AppendMenu(menu, DesktopNative.MF_STRING, new UIntPtr(CmdExit), "Ukončit Meteo…");

            DesktopNative.GetCursorPos(out var pt);
            // Bez popředí okna by se nabídka po kliknutí jinam nezavřela.
            DesktopNative.SetForegroundWindow(_source.Handle);
            var cmd = DesktopNative.TrackPopupMenuEx(
                menu, DesktopNative.TPM_RIGHTBUTTON | DesktopNative.TPM_RETURNCMD | DesktopNative.TPM_NONOTIFY,
                pt.X, pt.Y, _source.Handle, IntPtr.Zero);
            DesktopNative.PostMessage(_source.Handle, DesktopNative.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (cmd > 0) Command?.Invoke(cmd);
        }
        finally
        {
            DesktopNative.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = Data();
            DesktopNative.Shell_NotifyIcon(DesktopNative.NIM_DELETE, ref data);
            _added = false;
        }
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
