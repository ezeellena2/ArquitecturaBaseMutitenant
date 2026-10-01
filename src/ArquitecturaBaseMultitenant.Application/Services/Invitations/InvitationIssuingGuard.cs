using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Serializa destinos compartidos y comprueba organización y dueño sin crear identidades.</summary>
internal sealed class InvitationIssuingGuard(IUserLookup lookup, IExternalLoginLock loginLock,
    IInvitationRepository invitations, ITenantReader tenants)
{
    internal async Task<Result<(TenantRow Organization, Guid? UserId)>> CheckAsync(
        Guid tenantId, IssueInvitationRequest request, CancellationToken ct)
    {
        await loginLock.LockEmailAsync(request.Destination, ct);
        await invitations.LockDestinationAsync(request.Destination, ct);
        var tenant = await tenants.FindByIdAsync(tenantId, ct);
        if (tenant is null || tenant.Kind != TenantKind.Business) return InvitationErrors.Invalid;
        if (tenant.Status != TenantStatus.Active) return tenant.Status switch
        {
            TenantStatus.Suspended => TenantErrors.Suspended,
            TenantStatus.PendingApproval => TenantErrors.PendingApproval,
            TenantStatus.Provisioning => TenantErrors.Provisioning,
            _ => TenantErrors.Closed,
        };
        var owners = await lookup.FindVerifiedUsersByEmailAsync(request.Destination, ct);
        if (owners.Count > 1) return InvitationErrors.WrongAccount;
        return (tenant, owners.Count == 0 ? null : owners[0]);
    }
}
