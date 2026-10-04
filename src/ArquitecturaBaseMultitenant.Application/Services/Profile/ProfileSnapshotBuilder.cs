using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Compone la cuenta y sus accesos globales con las preferencias del tenant activo.</summary>
internal sealed class ProfileSnapshotBuilder(IUserRepository users, IUserTenantAccessReader accesses,
    ProfilePreferences preferences,
    ILoginMethodReader loginMethods, LoginMethodAvailability availability, LegalAcceptanceGuard legal,
    IPermissionService permissions, IFeatureService features)
{
    internal async Task<MeResponse?> BuildAsync(Guid userId, Access access, Guid? activeTenantId,
        CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null) return null;

        var memberships = await accesses.ListForUserAsync(userId, cancellationToken);
        var organizations = memberships.Where(row => row.Kind == TenantKind.Business)
            .Select(row => new OrganizationSummary(row.TenantId, row.Name, row.Slug,
                row.TenantStatus, row.RoleName, row.MemberStatus)).ToArray();
        var personalSpace = memberships.Any(row => row.Kind == TenantKind.Personal);
        var activeMembership = memberships.SingleOrDefault(row => row.TenantId == activeTenantId);
        var mayReadSettings = activeMembership is
        {
            TenantStatus: TenantStatus.Active,
            MemberStatus: MemberStatus.Active,
        };
        var currencyCode = await preferences.FindCurrencyCodeAsync(account.Culture, mayReadSettings, cancellationToken);
        var methods = await loginMethods.ListByUserIdAsync(userId, cancellationToken);
        var hasPersonalMethod = await availability.HasPersonalBackupAsync(userId, methods, cancellationToken);
        var readsPermissions = access == Access.Business && mayReadSettings;
        IReadOnlyList<string> organizationPermissions = readsPermissions
            ? await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken)
            : [];
        var companyPermissions = readsPermissions
            ? await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken)
            : new Dictionary<Guid, IReadOnlyList<string>>();

        return new MeResponse(account.Id, account.DisplayName, account.PrimaryEmail, access,
            activeTenantId, personalSpace, organizations,
            new EffectivePermissions(organizationPermissions, companyPermissions),
            account.Culture, account.TimeZoneId, currencyCode,
            mayReadSettings ? await features.ListEnabledAsync(cancellationToken) : [])
        {
            Version = account.Version,
            PendingLegalDocuments = await legal.PendingAsync(userId, cancellationToken),
            NeedsPersonalLoginMethod = !hasPersonalMethod,
        };
    }
}
