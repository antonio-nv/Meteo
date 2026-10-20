using System.Runtime.InteropServices;

namespace Meteo.Windows;

/// <summary>
/// Nízkoúrovňový hák klávesnice. Win+šipky vyřizuje shell dřív, než se dostanou do aplikace, takže jinak než hákem
/// je nelze ani zachytit, ani zakázat. Dokud je popředí naše okno, hák:
///  - Win+Shift+Vlevo / Vpravo: vyvolá <see cref="MoveRequested"/> (−1 / +1) a klávesu spolkne,
///  - Win+šipky (nahoru, dolů, vlevo, vpravo): spolkne, takže se okno nepřipne, neztmaví ani nezmenší na čtvrtinu.
/// Jiné klávesy a okna ostatních aplikací nejsou dotčeny. Používá se jen u samostatného okna, nikdy ve šetřiči.
/// </summary>
public sealed class ShellKeyHook : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // reference drží delegáta naživu
    private IntPtr _hook;
    private long _lastMove;

    /// <summary>−1 = na monitor vlevo, +1 = na monitor vpravo. Voláno na vlákně okna.</summary>
    public event Action<int>? MoveRequested;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public ShellKeyHook(IntPtr windowHandle)
    {
        _hwnd = windowHandle;
        _proc = Callback;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
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
                var isArrow = kb.vkCode is >= NativeMethods.VK_LEFT and <= NativeMethods.VK_DOWN;

                if (isArrow &&
                    (kb.flags & NativeMethods.LLKHF_INJECTED) == 0 &&
                    WinKeyDown() &&
                    NativeMethods.GetForegroundWindow() == _hwnd)
                {
                    if (isDown)
                    {
                        var shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                        var horizontal = kb.vkCode is NativeMethods.VK_LEFT or NativeMethods.VK_RIGHT;
                        if (shift && horizontal && Environment.TickCount64 - _lastMove > 250)
                        {
                            _lastMove = Environment.TickCount64;
                            MoveRequested?.Invoke(kb.vkCode == NativeMethods.VK_LEFT ? -1 : 1);
                        }
                        MaskStartMenu();
                    }
                    return (IntPtr)1; // spolknout
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool WinKeyDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

    /// <summary>
    /// Když spolkneme šipku, Windows po uvolnění Win vidí "Win stisknuto a puštěno bez jiné klávesy" a otevřou Start.
    /// Vložená nepřiřazená klávesa to zruší.
    /// </summary>
    private static void MaskStartMenu()
    {
        var inputs = new NativeMethods.Input[2];
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_MASK;
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_MASK;
        inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
