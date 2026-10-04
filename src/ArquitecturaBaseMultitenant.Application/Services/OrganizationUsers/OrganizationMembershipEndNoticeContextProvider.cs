using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

internal sealed record OrganizationMembershipEndNoticeContext(
    Guid UserId,
    UserAccountRow Account,
    TenantRow Tenant,
    string AccountUrl);

/// <summary>Registra el evento de seguridad y prepara los datos de la notificación de fin de membresía.</summary>
internal sealed class OrganizationMembershipEndNoticeContextProvider(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ISecurityEventRepository securityEvents,
    IUserRepository users,
    ITenantReader tenants,
    IPublicOrigin publicOrigin)
{
    internal async Task<Result<OrganizationMembershipEndNoticeContext?>> PrepareAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId || actorId == Guid.Empty) return AccessErrors.NotMember;
        var tenantId = tenantContext.RequiredTenantId;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        securityEvents.Add(SecurityEvent.ForOrganizationMembershipEnded(actorId, tenantId, userId, nowUtc));

        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status == UserStatus.Deleted)
            return Result.Success<OrganizationMembershipEndNoticeContext?>(null);
        var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null) return MemberErrors.NotFound;

        var accountUrl = publicOrigin.Value is { } origin ? new Uri(origin, "/cuenta").AbsoluteUri : "/cuenta";
        return new OrganizationMembershipEndNoticeContext(userId, account, tenant, accountUrl);
    }
}
