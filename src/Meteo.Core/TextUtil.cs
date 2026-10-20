using System.Globalization;
using System.Text;

namespace Meteo.Core;

public static class TextUtil
{
    /// <summary>Velká písmena bez diakritiky ("Přibyslav" → "PRIBYSLAV"), pro porovnávání a hledání.</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString().Trim();
    }

    /// <summary>Jen písmena a číslice, velkými písmeny, bez diakritiky ("Ústí nad Labem" → "USTINADLABEM").</summary>
    public static string Compact(string s) => new(Fold(s).Where(char.IsLetterOrDigit).ToArray());
}
