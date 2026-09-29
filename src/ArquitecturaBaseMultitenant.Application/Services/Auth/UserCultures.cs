using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resolves a notification culture from enabled reference data.</summary>
internal sealed class UserCultures(ICultureCatalog catalog)
{
    public async Task<CultureProfile> ResolveAsync(string? accountCulture, string? organizationCulture,
        CancellationToken cancellationToken)
    {
        var supported = await SupportedCultures.LoadAsync(catalog, cancellationToken);
        var code = IsEnabled(accountCulture, supported) ? accountCulture
            : IsEnabled(organizationCulture, supported) ? organizationCulture
            : supported.DefaultCulture;
        return await new CultureProfiles(catalog).LoadAsync(code, cancellationToken);
    }

    private static bool IsEnabled(string? code, SupportedCultures supported) =>
        code is not null && supported.Codes.Contains(code, StringComparer.OrdinalIgnoreCase);
}
