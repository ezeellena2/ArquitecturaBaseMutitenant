using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Resuelve los datos de referencia admitidos por la ficha de una empresa.</summary>
internal sealed class CompanyReferenceDataResolver(ITaxIdTypeCatalog taxIdTypes, ITimeZoneCatalog timeZones)
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

    public async Task<bool> IsAvailableTimeZoneAsync(string id, CancellationToken cancellationToken) =>
        await timeZones.FindAsync(id, cancellationToken) is { IsEnabled: true };
}
