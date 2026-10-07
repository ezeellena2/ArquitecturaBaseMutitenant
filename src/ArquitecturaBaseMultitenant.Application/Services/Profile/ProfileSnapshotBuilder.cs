using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Profile;

/// <summary>Compone la cuenta y sus accesos globales con las preferencias del tenant activo.</summary>
internal sealed class ProfileSnapshotBuilder(IUserRepository users, IUserTenantAccessReader accesses,
    ProfilePreferencesResolver preferences,
    ILoginMethodReader loginMethods, LoginMethodAvailabilityPolicy availability, LegalAcceptanceGuard legal,
    ProfileEffectivePermissionsBuilder permissions, IFeatureService features)
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
        var mayReadOrganizationCountry = mayReadSettings && access == Access.Business
            && activeMembership?.Kind == TenantKind.Business;
        var profilePreferences = await preferences.ResolveAsync(account.Culture, mayReadSettings,
            mayReadOrganizationCountry, cancellationToken);
        var methods = await loginMethods.ListByUserIdAsync(userId, cancellationToken);
        var hasPersonalMethod = await availability.HasPersonalBackupAsync(userId, methods, cancellationToken);
        var effectivePermissions = await permissions.BuildAsync(userId, access, mayReadSettings, cancellationToken);

        return new MeResponse(account.Id, account.DisplayName,
            ManagedEmail(methods, access, activeTenantId) ?? account.PrimaryEmail, access,
            activeTenantId, personalSpace, organizations,
            effectivePermissions,
            account.Culture, account.TimeZoneId, profilePreferences.CurrencyCode,
            mayReadSettings ? await features.ListEnabledAsync(cancellationToken) : [])
        {
            FirstName = account.FirstName,
            LastName = account.LastName,
            OrganizationCountryCode = profilePreferences.OrganizationCountryCode,
            Version = account.ProfileVersion,
            PendingLegalDocuments = await legal.PendingAsync(userId, cancellationToken),
            NeedsPersonalLoginMethod = !hasPersonalMethod,
        };
    }

    /// <summary>En una organización, el correo verificado que ella administra; en los demás perfiles, ninguno.</summary>
    private static Email? ManagedEmail(IReadOnlyList<LoginMethodRow> methods, Access access, Guid? activeTenantId) =>
        access == Access.Business && activeTenantId is { } tenantId
            ? methods.Where(method => method.Type == LoginMethodType.Email && method.ManagedByTenantId == tenantId
                    && method.VerifiedAtUtc is not null)
                .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                .Select(method => Email.Create(method.Value)).Where(email => email.IsSuccess)
                .Select(email => email.Value).FirstOrDefault()
            : null;
}
