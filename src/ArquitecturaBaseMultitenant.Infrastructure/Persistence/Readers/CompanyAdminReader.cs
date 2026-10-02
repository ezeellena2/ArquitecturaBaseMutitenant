using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Cuenta solo Administradores de empresa con membresía e identidad activas.</summary>
internal sealed class CompanyAdminReader(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyAdminReader
{
    public Task<int> CountActiveCompanyAdminsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("A company id is required.", nameof(companyId));
        var tenantId = tenantContext.RequiredTenantId;
        return (from membership in context.CompanyMemberships.AsNoTracking()
                join member in context.Members.AsNoTracking()
                    on new { membership.TenantId, MemberId = membership.MemberId }
                    equals new { member.TenantId, MemberId = member.Id }
                join user in context.Users.AsNoTracking() on member.UserId equals user.Id
                join assignment in context.RoleAssignments.AsNoTracking()
                    on membership.MemberId equals assignment.MemberId
                join role in context.Roles.AsNoTracking()
                    on new { assignment.TenantId, assignment.RoleId }
                    equals new { role.TenantId, RoleId = role.Id }
                where membership.TenantId == tenantId && membership.CompanyId == companyId
                    && assignment.TenantId == tenantId && assignment.CompanyId == companyId
                    && membership.Status == CompanyMembershipStatus.Active
                    && member.Status == MemberStatus.Active && user.Status == UserStatus.Active
                    && role.Name == SystemRoles.CompanyAdmin
                select membership.MemberId).Distinct().CountAsync(cancellationToken);
    }
}
