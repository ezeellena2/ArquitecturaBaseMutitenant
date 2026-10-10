using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Código de moneda con forma ISO 4217; el catálogo decide si existe y está habilitado.</summary>
public sealed class CurrencyCode : ValueObject
{
    public const int Length = 3;
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<CurrencyCode> Create(string? code)
    {
        if (code is not { Length: Length } || !code.All(IsAsciiLetter))
        {
            return CurrencyCodeErrors.Invalid;
        }

        return new CurrencyCode(code.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsAsciiLetter(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
