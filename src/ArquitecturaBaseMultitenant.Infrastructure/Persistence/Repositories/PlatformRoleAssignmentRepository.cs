using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Escrituras de autorización global dentro del límite transaccional del caso de uso.</summary>
internal sealed class PlatformRoleAssignmentRepository(ApplicationDbContext context) : IPlatformRoleAssignmentRepository
{
    public Task<PlatformRoleAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformRoleAssignments.SingleOrDefaultAsync(assignment => assignment.UserId == userId, cancellationToken);
    }

    public void Add(PlatformRoleAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformRoleAssignments.Add(assignment);
    }

    public void Remove(PlatformRoleAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformRoleAssignments.Remove(assignment);
    }
}
