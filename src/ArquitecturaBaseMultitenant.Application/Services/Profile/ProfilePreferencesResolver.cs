using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Resuelve moneda y país empresarial desde una sola lectura autorizada de preferencias y sus catálogos.</summary>
internal sealed class ProfilePreferencesResolver(ITenantSettingsReader settings, ICultureCatalog cultures,
    ICountryCatalog countries)
{
    internal async Task<(string? CurrencyCode, string? OrganizationCountryCode)> ResolveAsync(string cultureCode,
        bool mayReadTenantSettings, bool mayReadOrganizationCountry, CancellationToken cancellationToken)
    {
        var tenantSettings = mayReadTenantSettings
            ? await settings.FindCurrentAsync(cancellationToken) : null;
        var culture = await cultures.FindAsync(cultureCode, cancellationToken);
        var country = culture is null
            ? null : await countries.FindAsync(culture.CountryCode, cancellationToken);
        var organizationCulture = !mayReadOrganizationCountry || tenantSettings is null ? null
            : string.Equals(tenantSettings.DefaultCulture, cultureCode, StringComparison.OrdinalIgnoreCase)
                ? culture : await cultures.FindAsync(tenantSettings.DefaultCulture, cancellationToken);
        return (tenantSettings?.DefaultCurrency ?? country?.DefaultCurrencyCode, organizationCulture?.CountryCode);
    }
}
