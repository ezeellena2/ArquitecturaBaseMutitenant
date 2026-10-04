using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Escrituras de autorización global dentro del límite transaccional del caso de uso.</summary>
internal sealed class PlatformPermissionAssignmentRepository(ApplicationDbContext context)
    : IPlatformPermissionAssignmentRepository
{
    public Task<PlatformPermissionAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformPermissionAssignments.SingleOrDefaultAsync(
            assignment => assignment.UserId == userId, cancellationToken);
    }

    public void Add(PlatformPermissionAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformPermissionAssignments.Add(assignment);
    }

    public void Remove(PlatformPermissionAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformPermissionAssignments.Remove(assignment);
    }
}
