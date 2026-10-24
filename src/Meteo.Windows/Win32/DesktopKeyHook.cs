using System.Runtime.InteropServices;

namespace Meteo.Windows;

/// <summary>
/// Klávesy pro přepínání míst, když Meteo běží jako živé pozadí a nemá fokus (okno za ikonami klávesy nedostává).
/// Nízkoúrovňový hák klávesnice zachytí Vlevo / Vpravo:
///  - Ctrl+Alt+Vlevo / Vpravo kdekoli,
///  - samotné Vlevo / Vpravo jen tehdy, když je v popředí plocha (kliknutí na prázdné místo, Win+D).
/// V ostatních aplikacích hák šipky nikdy nespolkne.
/// </summary>
public sealed class DesktopKeyHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private long _last;

    /// <summary>−1 = předchozí místo, +1 = další. Voláno na vlákně, které hák nainstalovalo.</summary>
    public event Action<int>? StepRequested;

    /// <summary>Stisknuto Win+D (zobrazit plochu). Klávesa se nespolkne, Windows ji vyřídí normálně.</summary>
    public event Action? ShowDesktopPressed;

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>Win+D bez dalších modifikátorů (Win+Ctrl+D je nová virtuální plocha a nesmí Meteo ukončit).</summary>
    public static bool IsShowDesktop(uint vkCode, bool ctrl, bool alt, bool shift, bool win) =>
        vkCode == 0x44 && win && !ctrl && !alt && !shift;

    public DesktopKeyHook()
    {
        _proc = Callback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    /// <summary>Třída okna v popředí, které patří ploše (Průzkumník).</summary>
    public static bool IsDesktopClass(string? className) => className is "Progman" or "WorkerW";

    /// <summary>Má se šipka vyřídit (a spolknout)? Čistá funkce, aby šla otestovat bez Windows.</summary>
    public static bool ShouldHandle(bool ctrl, bool alt, bool shift, bool win, string? foregroundClass)
    {
        if (shift || win) return false;
        if (ctrl && alt) return true;
        return !ctrl && !alt && IsDesktopClass(foregroundClass);
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam.ToInt64();
            var isDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            var isUp = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
            if (isDown || isUp)
            {
                var kb = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
                if (isDown && (kb.flags & NativeMethods.LLKHF_INJECTED) == 0 &&
                    IsShowDesktop(kb.vkCode, Pressed(0x11), Pressed(0x12), Pressed(NativeMethods.VK_SHIFT),
                                  Pressed(NativeMethods.VK_LWIN) || Pressed(NativeMethods.VK_RWIN)))
                    ShowDesktopPressed?.Invoke();
                if ((kb.vkCode == NativeMethods.VK_LEFT || kb.vkCode == NativeMethods.VK_RIGHT) &&
                    (kb.flags & NativeMethods.LLKHF_INJECTED) == 0 &&
                    ShouldHandle(
                        Pressed(0x11),                       // Ctrl
                        Pressed(0x12),                       // Alt
                        Pressed(NativeMethods.VK_SHIFT),
                        Pressed(NativeMethods.VK_LWIN) || Pressed(NativeMethods.VK_RWIN),
                        DesktopNative.ClassNameOf(NativeMethods.GetForegroundWindow())))
                {
                    if (isDown && Environment.TickCount64 - _last > 150)
                    {
                        _last = Environment.TickCount64;
                        StepRequested?.Invoke(kb.vkCode == NativeMethods.VK_LEFT ? -1 : 1);
                    }
                    return (IntPtr)1; // spolknout stisk i uvolnění, ať nepřijdou ikonám plochy ani jiné aplikaci
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool Pressed(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
