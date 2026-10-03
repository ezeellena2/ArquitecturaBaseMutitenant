using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resuelve CUIT y preferencias iniciales desde los catálogos habilitados.</summary>
internal sealed class BusinessSignupRequestResolver(
    ITaxIdTypeCatalog taxIdTypes,
    IPersonalSpaceProvisioner defaults)
{
    internal async Task<Result<TaxId?>> CreateTaxIdAsync(BusinessSignupTaxId? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return Result.Success<TaxId?>(null);
        var type = await taxIdTypes.FindAsync(request.Type ?? string.Empty, cancellationToken);
        if (type is not { IsEnabled: true } || type.AppliesTo is not ("Company" or "Both"))
            return TaxIdErrors.Invalid;
        var domainType = TaxIdType.Create(type.Code, type.CountryCode, type.Label, type.Mask,
            type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);
        var taxId = TaxId.Create(type.CountryCode, domainType, request.Number);
        return taxId.IsSuccess ? Result.Success<TaxId?>(taxId.Value) : taxId.Error;
    }

    internal Task<PersonalSpaceDraft> PrepareTenantSpaceAsync(string? culture, string? timeZoneId,
        CancellationToken cancellationToken) => defaults.PrepareAsync(culture, timeZoneId, cancellationToken);
}
