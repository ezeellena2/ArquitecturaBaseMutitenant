using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Services.Auth;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Elige preferencias desde la organización y catálogos; nunca prepara un espacio Personal.</summary>
internal sealed class InvitationIdentityDefaults(ITenantSettingsReader settings, UserCultures cultures,
    ICountryCatalog countries, ITimeZoneCatalog timeZones)
{
    internal async Task<(string Culture, string TimeZoneId)> ReadAsync(CancellationToken ct)
    {
        var organization = await settings.FindCurrentAsync(ct);
        var profile = await cultures.ResolveAsync(null, organization?.DefaultCulture, ct);
        var country = await countries.FindAsync(profile.Entry.CountryCode, ct);
        var preferred = organization?.DefaultTimeZoneId;
        var zone = preferred is null ? null : await timeZones.FindAsync(preferred, ct);
        if (zone is not { IsEnabled: true })
            zone = country?.DefaultTimeZoneId is { } fallback ? await timeZones.FindAsync(fallback, ct) : null;
        if (zone is not { IsEnabled: true }) throw new InvalidOperationException("The invitation culture requires an enabled time zone.");
        return (profile.Entry.Code, zone.Id);
    }
}
