using System.Globalization;
using System.Net;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Nombre DNS normalizado para identificar los correos de una organización.</summary>
public sealed class OrganizationEmailDomain : ValueObject
{
    public const int MaxLength = 253;

    private OrganizationEmailDomain(string value) => Value = value;

    public string Value { get; }

    public static Result<OrganizationEmailDomain> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TenantDomainErrors.InvalidDomain;
        var normalized = value.Trim();
        if (normalized[^1] == '.') normalized = normalized[..^1];
        if (normalized.Length is 0 or > MaxLength || normalized[^1] == '.')
            return TenantDomainErrors.InvalidDomain;

        string ascii;
        try
        {
            ascii = new IdnMapping { UseStd3AsciiRules = true }.GetAscii(normalized).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return TenantDomainErrors.InvalidDomain;
        }

        if (ascii.Length > MaxLength || IPAddress.TryParse(ascii, out _)) return TenantDomainErrors.InvalidDomain;
        var labels = ascii.Split('.');
        if (labels.Length < 2 || labels.Any(label => label.Length is 0 or > 63
            || label[0] == '-' || label[^1] == '-'
            || label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            return TenantDomainErrors.InvalidDomain;
        }

        return new OrganizationEmailDomain(ascii);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
