using Microsoft.Win32;

namespace Meteo.Windows;

/// <summary>Spuštění živého pozadí po přihlášení do Windows (klíč Run v registru, jen pro aktuálního uživatele, bez práv správce).</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Meteo";

    /// <summary>Příkaz, který se zapíše do registru.</summary>
    public static string Command(string exePath) => "\"" + exePath + "\" /d";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <returns>false, pokud se zápis do registru nepovedl.</returns>
    public static bool Set(bool enabled, string? exePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var path = exePath ?? Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return false;
                key.SetValue(ValueName, Command(path));
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
