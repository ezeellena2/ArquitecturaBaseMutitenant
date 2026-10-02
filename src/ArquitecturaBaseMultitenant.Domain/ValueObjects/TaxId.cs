using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects.TaxIdValidators;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Identificación fiscal normalizada como país, tipo de catálogo y número sin separadores.</summary>
public sealed class TaxId : ValueObject
{
    private TaxId(string country, string type, string number)
    {
        Country = country;
        Type = type;
        Number = number;
    }

    public string Country { get; }
    public string Type { get; }
    public string Number { get; }

    public static Result<TaxId> Create(string? country, TaxIdType? type, string? numberText)
    {
        if (country is not { Length: 2 } || !country.All(IsAsciiLetter)
            || type is null || numberText is null)
        {
            return TaxIdErrors.Invalid;
        }

        var countryCode = country.ToUpperInvariant();
        var typeCode = type.Code.ToUpperInvariant();
        if (!string.Equals(type.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)
            || typeCode.Length > 16 || !typeCode.StartsWith(countryCode + "-", StringComparison.Ordinal))
        {
            return TaxIdErrors.Invalid;
        }

        var number = new System.Text.StringBuilder(numberText.Length);
        foreach (var character in numberText)
        {
            if (IsAsciiDigit(character)) number.Append(character);
            else if (character is '-' or '.' || char.IsWhiteSpace(character)) continue;
            else return TaxIdErrors.Invalid;
        }

        var digits = number.ToString();
        var valid = type.ValidatorKey switch
        {
            "ar-cuit-mod11" => ArgentineCuitValidator.IsValid(digits),
            "ar-dni-length" => digits.Length is 7 or 8,
            _ => false,
        };

        return valid ? new TaxId(countryCode, typeCode, digits) : TaxIdErrors.Invalid;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Country;
        yield return Type;
        yield return Number;
    }

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
