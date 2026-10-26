using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Meteo.Windows;

/// <summary>
/// Hlídá velikost paměti. Program stále něco kreslí a vytváří krátkodobé objekty; .NET je uklízí až při velké zásobě
/// a Správce úloh pak ukazuje stovky MB, i když program skutečně drží zlomek. Janitor v pravidelných intervalech
/// provede úklid a nechá Windows odebrat nepoužívané stránky z pracovní sady (ty se při použití samy vrátí).
/// </summary>
public sealed class MemoryJanitor : IDisposable
{
    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private readonly Timer _timer;

    public MemoryJanitor(TimeSpan interval) =>
        _timer = new Timer(_ => Clean(), null, interval, interval);

    /// <summary>Úklid hned: plná kolekce s komprimací a zmenšení pracovní sady.</summary>
    public static void Clean()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            // jen kosmetika, nesmí nic shodit
        }
    }

    /// <summary>Text pro okno Informace: skutečná spravovaná paměť vs. pracovní sada, kterou ukazuje Správce úloh.</summary>
    public static string Describe()
    {
        using var process = Process.GetCurrentProcess();
        var mb = 1024.0 * 1024.0;
        return $"Paměť: spravovaná halda {GC.GetTotalMemory(false) / mb:0} MB, " +
               $"pracovní sada {process.WorkingSet64 / mb:0} MB, soukromá {process.PrivateMemorySize64 / mb:0} MB, " +
               $"kolekce 0/1/2: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}";
    }

    public void Dispose() => _timer.Dispose();
}
