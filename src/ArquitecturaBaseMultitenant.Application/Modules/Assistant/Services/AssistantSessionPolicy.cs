using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantSessionPolicy(ICurrentUser user, ITenantContext tenant, IUserRepository accounts,
    IAccessStatusCache statuses, ITenantReader tenants, ILegalAcceptanceService legal, IUserStatusReader identities)
{
    internal async Task<bool> IsActiveAsync(CancellationToken ct)
    {
        if (user.UserId is not { } id || user.Access is not { } current) return false;
        var account = await accounts.GetByIdAsync(id, ct);
        if (account is null || await identities.GetStatusAsync(id, ct) != UserStatus.Active || account.HasPendingOperatorInvitation
            || account.IsPlatformOperator != (current == Access.Platform)) return false;
        if (current != Access.Platform)
        {
            var kind = current == Access.Consumer ? TenantKind.Personal : TenantKind.Business;
            if (tenant.TenantId is not { } tenantId || tenant.TenantKind != kind
                || await tenants.FindByIdAsync(tenantId, ct) is not { Status: TenantStatus.Active, Kind: var actual } || actual != kind
                || await statuses.GetMemberStatusAsync(id, tenantId, kind, ct) != MemberStatus.Active) return false;
        }
        var pending = await legal.GetPendingAsync(ct);
        return pending.IsSuccess && pending.Value.Count == 0;
    }
}
