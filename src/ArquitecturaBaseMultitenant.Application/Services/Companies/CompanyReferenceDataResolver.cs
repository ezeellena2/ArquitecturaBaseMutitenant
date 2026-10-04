using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Resuelve los datos de referencia admitidos por la ficha de una empresa.</summary>
internal sealed class CompanyReferenceDataResolver(ITaxIdTypeCatalog taxIdTypes, ITimeZoneCatalog timeZones,
    ITenantSettingsReader settings, ICultureCatalog cultures)
{
    /// <summary>
    /// D6: la ficha pide solo el número. Si no cambió, se conserva el tipo guardado, aunque la organización haya
    /// cambiado de país. Si no, el tipo es el de empresas del país de la organización, que es la región de su
    /// «Idioma y región» (CUIT en Argentina). Un país sin ese tipo no admite un número.
    /// </summary>
    public async Task<Result<TaxId?>> ResolveTaxIdAsync(TaxId? current, string? number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number)) return Result.Success<TaxId?>(null);
        if (current is not null && await taxIdTypes.FindAsync(current.Type, cancellationToken) is { } currentType
            && TaxId.Create(current.Country, ToDomain(currentType), number) is { IsSuccess: true } same
            && same.Value == current)
            return current;
        var type = await DefaultCompanyTypeAsync(cancellationToken);
        if (type is null) return TaxIdErrors.Invalid;
        var result = TaxId.Create(type.CountryCode, ToDomain(type), number);
        return result.IsSuccess ? Result.Success<TaxId?>(result.Value) : TaxIdErrors.Invalid;
    }

    /// <summary>El primer tipo habilitado para empresas del país, por SortOrder y después por código.</summary>
    private async Task<TaxIdTypeCatalogEntry?> DefaultCompanyTypeAsync(CancellationToken cancellationToken)
    {
        var organization = await settings.FindCurrentAsync(cancellationToken);
        var culture = organization is null ? null : await cultures.FindAsync(organization.DefaultCulture, cancellationToken);
        if (culture is null) return null;
        return (await taxIdTypes.ListAsync(cancellationToken))
            .Where(type => type.IsEnabled && type.AppliesTo is "Company" or "Both"
                && string.Equals(type.CountryCode, culture.CountryCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(type => type.SortOrder ?? int.MaxValue).ThenBy(type => type.Code, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static TaxIdType ToDomain(TaxIdTypeCatalogEntry type) => TaxIdType.Create(type.Code, type.CountryCode,
        type.Label, type.Mask, type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);

    /// <summary>Sin zona elegida, la empresa toma la de la organización («La de la organización» en el tablero).</summary>
    public async Task<Result<string>> ResolveTimeZoneAsync(string? requested, CancellationToken cancellationToken)
    {
        var id = string.IsNullOrWhiteSpace(requested)
            ? (await settings.FindCurrentAsync(cancellationToken))?.DefaultTimeZoneId
            : requested;
        return id is not null && await timeZones.FindAsync(id, cancellationToken) is { IsEnabled: true }
            ? id
            : CompanyErrors.TimeZoneUnavailable;
    }
}
