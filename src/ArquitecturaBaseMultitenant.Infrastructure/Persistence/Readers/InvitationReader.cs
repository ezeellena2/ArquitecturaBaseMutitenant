using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
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
                invitation.BootstrapNonceHash, inviter.Status == UserStatus.Deleted ? null : inviter.DisplayName,
                inviter.Status == UserStatus.Deleted, invitation.InviterUserId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<InvitationGrantRow> FindGrantAsync(Guid memberId, Guid? inviterUserId,
        CancellationToken cancellationToken)
    {
        _ = tenantContext.RequiredTenantId;
        var inviterMembers = context.Members.AsNoTracking()
            .Where(member => inviterUserId != null && member.UserId == inviterUserId && member.Status == MemberStatus.Active)
            .Select(member => member.Id);
        var invitedMembers = context.Members.AsNoTracking().Where(member => member.Id == memberId)
            .Select(member => member.Id);
        var companies = await (from membership in context.CompanyMemberships.AsNoTracking()
                where membership.MemberId == memberId && membership.Status == CompanyMembershipStatus.Active
                join company in context.Companies.AsNoTracking() on membership.CompanyId equals company.Id
                orderby company.Name
                select company.Name)
            .ToListAsync(cancellationToken);
        // De quien invita se muestra su rol en la organización («Dueño»); si solo tiene roles de empresa, esos.
        var inviterRoles = await RolesAsync(inviterMembers, cancellationToken);
        var inviterOrganizationRoles = inviterRoles.Where(role => role.InOrganization).ToList();
        return new InvitationGrantRow(Names(inviterOrganizationRoles.Count > 0 ? inviterOrganizationRoles : inviterRoles),
            companies, Names(await RolesAsync(invitedMembers, cancellationToken)));
    }

    private Task<List<GrantedRole>> RolesAsync(IQueryable<Guid> memberIds, CancellationToken cancellationToken) =>
        (from assignment in context.RoleAssignments.AsNoTracking()
            where memberIds.Contains(assignment.MemberId)
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            orderby assignment.CompanyId != null, role.Name
            select new GrantedRole(role.Name, assignment.CompanyId == null))
        .ToListAsync(cancellationToken);

    private static string[] Names(IEnumerable<GrantedRole> roles) =>
        [.. roles.Select(role => role.Name).Distinct(StringComparer.Ordinal)];

    private sealed record GrantedRole(string Name, bool InOrganization);
}
