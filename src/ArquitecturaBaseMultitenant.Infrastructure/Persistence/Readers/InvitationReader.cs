using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta la invitación y sus datos actuales únicamente dentro del tenant ya autorizado.</summary>
internal sealed class InvitationReader(ApplicationDbContext context, ITenantContext tenantContext) : IInvitationReader
{
    public Task<InvitationRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _ = tenantContext.RequiredTenantId;
        return (from invitation in context.Invitations.AsNoTracking()
            where invitation.Id == id
            join member in context.Members.AsNoTracking()
                on new { invitation.TenantId, Id = invitation.MemberId } equals new { member.TenantId, member.Id }
            join tenant in context.Tenants.AsNoTracking() on invitation.TenantId equals tenant.Id
            join inviter in context.Users.AsNoTracking() on invitation.InviterUserId equals inviter.Id
            select new InvitationRow(invitation.Id, invitation.MemberId, member.UserId, member.Status,
                invitation.TenantId, tenant.Kind, tenant.Status, tenant.Name, invitation.Destination,
                invitation.Channel, invitation.TokenHash, invitation.Status, invitation.IssuedAtUtc,
                invitation.ExpiresAtUtc, invitation.AcceptedAtUtc, invitation.AcceptedByUserId,
                invitation.BootstrapNonceHash, inviter.Status == UserStatus.Deleted ? null : inviter.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
