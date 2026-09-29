using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Compone la cuenta y sus accesos globales con las preferencias del tenant activo.</summary>
internal sealed class ProfileSnapshotBuilder(IUserRepository users, IUserTenantAccessReader accesses,
    ITenantSettingsReader settings, ICultureCatalog cultures, ICountryCatalog countries)
{
    internal async Task<MeResponse?> BuildAsync(Guid userId, Access access, Guid? activeTenantId,
        CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null) return null;

        var memberships = await accesses.ListForUserAsync(userId, cancellationToken);
        var organizations = memberships.Where(row => row.Kind == TenantKind.Business)
            .Select(row => new OrganizationSummary(row.TenantId, row.Name, row.Slug,
                row.TenantStatus, null, row.MemberStatus)).ToArray();
        var personalSpace = memberships.Any(row => row.Kind == TenantKind.Personal);
        var activeMembership = memberships.SingleOrDefault(row => row.TenantId == activeTenantId);
        var mayReadSettings = activeMembership is
        {
            TenantStatus: TenantStatus.Active,
            MemberStatus: MemberStatus.Active,
        };
        var tenantSettings = mayReadSettings
            ? await settings.FindCurrentAsync(cancellationToken) : null;
        var culture = await cultures.FindAsync(account.Culture, cancellationToken);
        var country = culture is null
            ? null : await countries.FindAsync(culture.CountryCode, cancellationToken);
        var currencyCode = tenantSettings?.DefaultCurrency ?? country?.DefaultCurrencyCode;

        return new MeResponse(account.Id, account.DisplayName, account.PrimaryEmail, access,
            activeTenantId, personalSpace, organizations, EffectivePermissions.Empty,
            account.Culture, account.TimeZoneId, currencyCode, []);
    }
}
