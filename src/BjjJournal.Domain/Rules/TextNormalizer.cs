using System.Globalization;
using System.Text;

namespace BjjJournal.Domain.Rules;

/// <summary>Normaliza texto para comparar sin mayúsculas ni tildes ("Kimura" == "kímura").</summary>
public static class TextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var decomposed = text.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool Contains(string? text, string normalizedQuery) =>
        normalizedQuery.Length == 0 || Normalize(text).Contains(normalizedQuery, StringComparison.Ordinal);
}
