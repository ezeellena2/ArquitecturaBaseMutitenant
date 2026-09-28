using System.Globalization;
using System.Text;

namespace ArquitecturaBaseMultitenant.Application.Common.Text;

public static class TextNormalizer
{
    public static string? Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var builder = new StringBuilder(text.Length);
        Span<char> utf16 = stackalloc char[2];

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (rune.Value is not ('\n' or '\t') && category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                continue;
            }

            builder.Append(utf16[..rune.EncodeToUtf16(utf16)]);
        }

        var cleaned = builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    public static string? CleanName(string? text)
    {
        var cleaned = Clean(text);
        if (cleaned is null)
        {
            return null;
        }

        var builder = new StringBuilder(cleaned.Length);
        var pendingSpace = false;
        Span<char> utf16 = stackalloc char[2];

        foreach (var rune in cleaned.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(utf16[..rune.EncodeToUtf16(utf16)]);
        }

        return builder.ToString();
    }
}
