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
            if (rune.Value is not ('\n' or '\t') &&
                (category == UnicodeCategory.Control || IsUnsafeInvisible(rune.Value)))
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

    private static bool IsUnsafeInvisible(int scalar) => scalar switch
    {
        0x00AD or 0x061C or 0x200B or 0x200E or 0x200F or 0x2060 or 0xFEFF => true,
        >= 0x202A and <= 0x202E => true,
        >= 0x2066 and <= 0x2069 => true,
        _ => false
    };
}
