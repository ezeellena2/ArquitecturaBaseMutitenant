using System.Globalization;
using System.Text;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Correo normalizado para comparar y enviar; el dominio IDN se guarda en ASCII.</summary>
public sealed class Email : ValueObject
{
    public const int MaxLength = 254;
    private const int MaxLocalPartBytes = 64;

    private Email(string value, string displayValue)
    {
        Value = value;
        DisplayValue = displayValue;
    }

    /// <summary>Valor canónico para persistencia, comparación y envío.</summary>
    public string Value { get; }

    /// <summary>Valor en Unicode legible para mostrar, con el dominio IDN decodificado.</summary>
    public string DisplayValue { get; }

    public static Result<Email> Create(string? value)
    {
        if (value is null)
        {
            return EmailErrors.Invalid;
        }

        try
        {
            var normalized = value.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            var at = normalized.IndexOf('@', StringComparison.Ordinal);
            if (!HasValidFormat(normalized, at))
            {
                return EmailErrors.Invalid;
            }

            var localPart = normalized[..at];
            var domain = normalized[(at + 1)..];
            var idn = new IdnMapping();
            var asciiDomain = idn.GetAscii(domain).ToLowerInvariant();
            if (!HasValidAsciiDomain(asciiDomain))
            {
                return EmailErrors.Invalid;
            }

            var canonical = localPart + "@" + asciiDomain;
            if (Encoding.UTF8.GetByteCount(canonical) > MaxLength)
            {
                return EmailErrors.Invalid;
            }

            var displayDomain = idn.GetUnicode(asciiDomain).Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return new Email(canonical, localPart + "@" + displayDomain);
        }
        catch (ArgumentException)
        {
            return EmailErrors.Invalid;
        }
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool HasValidFormat(string value, int at)
    {
        if (at < 1
            || at != value.LastIndexOf('@')
            || at >= value.Length - 1
            || value.Any(character => char.IsWhiteSpace(character)
                || char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate))
        {
            return false;
        }

        var local = value[..at];
        if (Encoding.UTF8.GetByteCount(local) > MaxLocalPartBytes
            || local[0] == '.' || local[^1] == '.' || local.Contains("..", StringComparison.Ordinal)
            || local.Any(character => character is ',' or ';' or '<' or '>' or '(' or ')' or '[' or ']'
                or '"' or '\\' or ':'))
        {
            return false;
        }

        var domain = value[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal)
            && domain[0] != '.'
            && domain[^1] != '.'
            && !domain.Contains("..", StringComparison.Ordinal);
    }

    private static bool HasValidAsciiDomain(string domain)
    {
        foreach (var label in domain.Split('.'))
        {
            if (label.Length is < 1 or > 63
                || !IsAsciiLetterOrDigit(label[0])
                || !IsAsciiLetterOrDigit(label[^1])
                || label.Any(character => !IsAsciiLetterOrDigit(character) && character != '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or >= '0' and <= '9';
}
