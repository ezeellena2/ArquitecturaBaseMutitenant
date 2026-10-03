using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class TenantProvisioningReader(ApplicationDbContext context) : ITenantProvisioningReader
{
    public async Task<TenantProvisioningSnapshot> FindAsync(Guid? registeringUserId, CancellationToken cancellationToken)
    {
        var admins = from assignment in context.RoleAssignments.AsNoTracking()
                     join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                     where role.IsSystemRole && role.Name == SystemRoles.TenantAdmin && assignment.CompanyId == null
                     select assignment.MemberId;
        var owner = await context.Members.AsNoTracking()
            .Where(m => (m.Status == MemberStatus.Invited || m.Status == MemberStatus.Active) && admins.Contains(m.Id))
            .OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(cancellationToken);
        if (owner is null && registeringUserId is { } registrant)
            owner = await context.Members.AsNoTracking().Where(m => m.UserId == registrant && m.Status == MemberStatus.Active)
                .Select(m => (Guid?)m.Id).SingleOrDefaultAsync(cancellationToken);
        if (owner is null && registeringUserId is null)
        {
            var invited = await context.Members.AsNoTracking().Where(m => m.Status == MemberStatus.Invited)
                .OrderBy(m => m.Id).Select(m => m.Id).Take(2).ToListAsync(cancellationToken);
            if (invited.Count == 1) owner = invited[0];
        }
        Guid? company = null;
        if (owner is { } memberId)
            company = await context.CompanyMemberships.AsNoTracking().Where(m => m.MemberId == memberId && m.Status == CompanyMembershipStatus.Active)
                .OrderBy(m => m.CompanyId).Select(m => (Guid?)m.CompanyId).FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            var initial = await context.Companies.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).Take(2).ToListAsync(cancellationToken);
            if (initial.Count == 1) company = initial[0];
        }
        return new(owner, company);
    }
}
