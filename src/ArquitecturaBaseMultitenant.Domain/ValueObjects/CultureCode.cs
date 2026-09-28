using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Etiqueta idioma-región BCP 47; el catálogo decide si está habilitada.</summary>
public sealed class CultureCode : ValueObject
{
    private CultureCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<CultureCode> Create(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return CultureCodeErrors.Invalid;
        }

        var parts = code.Split('-');

        if (parts.Length is < 2 or > 3 || !IsAsciiLetters(parts[0], 2, 8))
        {
            return CultureCodeErrors.Invalid;
        }

        var hasScript = parts.Length == 3;
        if (hasScript && !IsAsciiLetters(parts[1], 4, 4))
        {
            return CultureCodeErrors.Invalid;
        }

        var region = parts[^1];
        if (!IsAsciiLetters(region, 2, 2) && !IsAsciiDigits(region, 3))
        {
            return CultureCodeErrors.Invalid;
        }

        var language = parts[0].ToLowerInvariant();
        var canonicalRegion = region.ToUpperInvariant();
        if (!hasScript)
        {
            return new CultureCode($"{language}-{canonicalRegion}");
        }

        var script = parts[1].ToLowerInvariant();
        var canonicalScript = char.ToUpperInvariant(script[0]) + script[1..];
        return new CultureCode($"{language}-{canonicalScript}-{canonicalRegion}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsAsciiLetters(string value, int minLength, int maxLength) =>
        value.Length >= minLength
        && value.Length <= maxLength
        && value.All(static letter => letter is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool IsAsciiDigits(string value, int length) =>
        value.Length == length
        && value.All(static digit => digit is >= '0' and <= '9');
}
