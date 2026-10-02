using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara cambios de asignación sin cruzar el tenant activo.</summary>
internal sealed class RoleAssignmentRepository(ApplicationDbContext context, ITenantContext tenantContext)
    : IRoleAssignmentRepository
{
    public async Task<IReadOnlyList<RoleAssignment>> ListForMemberAsync(Guid memberId,
        CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return await context.RoleAssignments.Where(assignment => assignment.TenantId == tenantId
            && assignment.MemberId == memberId).ToArrayAsync(cancellationToken);
    }

    public void Add(RoleAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.RoleAssignments.Add(assignment);
    }

    public void Remove(RoleAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.RoleAssignments.Remove(assignment);
    }
}
