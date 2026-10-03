using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Resuelve los datos de referencia admitidos por la ficha de una empresa.</summary>
internal sealed class CompanyReferenceDataResolver(ITaxIdTypeCatalog taxIdTypes, ITimeZoneCatalog timeZones,
    ITenantSettingsReader settings)
{
    public async Task<Result<TaxId?>> CreateTaxIdAsync(string? typeCode, string? number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeCode) && string.IsNullOrWhiteSpace(number))
            return Result.Success<TaxId?>(null);
        var type = await taxIdTypes.FindAsync(typeCode ?? string.Empty, cancellationToken);
        if (type is not { IsEnabled: true } || type.AppliesTo is not ("Company" or "Both"))
            return TaxIdErrors.Invalid;
        var domainType = TaxIdType.Create(type.Code, type.CountryCode, type.Label, type.Mask,
            type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);
        var result = TaxId.Create(type.CountryCode, domainType, number);
        return result.IsSuccess ? Result.Success<TaxId?>(result.Value) : TaxIdErrors.Invalid;
    }

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
