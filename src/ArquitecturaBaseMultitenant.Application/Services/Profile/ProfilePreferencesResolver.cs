using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Resuelve la moneda visible del perfil desde preferencias autorizadas y catálogos habilitados.</summary>
internal sealed class ProfilePreferencesResolver(ITenantSettingsReader settings, ICultureCatalog cultures,
    ICountryCatalog countries)
{
    internal async Task<string?> FindCurrencyCodeAsync(string cultureCode, bool mayReadTenantSettings,
        CancellationToken cancellationToken)
    {
        var tenantSettings = mayReadTenantSettings
            ? await settings.FindCurrentAsync(cancellationToken) : null;
        var culture = await cultures.FindAsync(cultureCode, cancellationToken);
        var country = culture is null
            ? null : await countries.FindAsync(culture.CountryCode, cancellationToken);
        return tenantSettings?.DefaultCurrency ?? country?.DefaultCurrencyCode;
    }
}
