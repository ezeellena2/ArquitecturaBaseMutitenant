using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Dirección de subdominio normalizada según el patrón de Registro-Empresa.</summary>
public sealed class TenantSlug : ValueObject
{
    public const int MaxLength = 63;

    private TenantSlug(string value) => Value = value;

    public string Value { get; }

    public static Result<TenantSlug> Create(string? value)
    {
        if (value is null) return TenantSlugErrors.Invalid;
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is 0 or 2 or > MaxLength
            || !IsAsciiLetterOrDigit(normalized[0])
            || !IsAsciiLetterOrDigit(normalized[^1])
            || normalized.Any(character => !IsAsciiLetterOrDigit(character) && character != '-'))
        {
            return TenantSlugErrors.Invalid;
        }

        return new TenantSlug(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
