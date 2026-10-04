using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Decide, con la cuenta bloqueada, si el pedido repite un alta ya hecha o si la cuenta puede crear la organización:
/// estado de la cuenta, modo de alta, límite de organizaciones propias y slug libre.
/// </summary>
internal sealed class BusinessSignupPolicy(
    ILoginMethodRepository loginMethods,
    IUserRepository users,
    ITenantRepository tenants,
    ITenantAdminReader tenantAdmins,
    IPlatformSettingsReader platformSettings,
    ITenantSlugAvailabilityReader slugs)
{
    internal async Task<Result<Decision>> DecideAsync(Guid userId, Guid tenantId, TenantSlug slug,
        CancellationToken cancellationToken)
    {
        await loginMethods.LockUserAsync(userId, cancellationToken);
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status == UserStatus.Deleted)
            return BusinessSignupErrors.IdentityRequired;
        if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (account.Status == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
        // ADR 0042: un operador nunca queda como Dueño de una organización.
        if (account.IsPlatformOperator) return AccessErrors.PlatformOnly;

        // Un pedido repetido no vuelve a evaluar el límite ni el slug: ya son de la organización que creó.
        var existing = await tenants.GetByIdAsync(tenantId, cancellationToken);
        if (existing is not null)
            return new Decision(account, existing, existing.Status == TenantStatus.PendingApproval);

        var settings = await platformSettings.FindAsync(cancellationToken)
            ?? throw new InvalidOperationException("PlatformSettings has not been seeded.");
        var owned = await tenantAdmins.ListOwnedBusinessTenantIdsAsync(userId, cancellationToken);
        var allowed = PlatformSettings.Create(ConsumerSignupMode.Open, settings.BusinessSignup,
            settings.MaxOwnedOrganizations).CanCreateBusinessOrganization(owned.Count);
        if (allowed.IsFailure) return allowed.Error;

        if (await slugs.IsReservedAsync(slug.Value, cancellationToken)) return TenantSlugErrors.Reserved;
        if (await slugs.IsTakenAsync(slug.Value, cancellationToken)) return TenantSlugErrors.Taken;
        return new Decision(account, null, settings.BusinessSignup == BusinessSignupMode.RequiresApproval);
    }

    /// <summary>La cuenta bloqueada, la organización si el pedido se repite y si el alta espera aprobación.</summary>
    internal sealed record Decision(UserAccountRow Account, Tenant? Existing, bool RequiresApproval);
}
