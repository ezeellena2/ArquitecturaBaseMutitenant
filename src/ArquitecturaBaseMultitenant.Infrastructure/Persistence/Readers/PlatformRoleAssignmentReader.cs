using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyección de la asignación global vigente; no cachea permisos revocables.</summary>
internal sealed class PlatformRoleAssignmentReader(ApplicationDbContext context) : IPlatformRoleAssignmentReader
{
    public Task<PlatformRole?> FindRoleAsync(Guid userId, CancellationToken cancellationToken) =>
        context.PlatformRoleAssignments.AsNoTracking().Where(assignment => assignment.UserId == userId)
            .Select(assignment => (PlatformRole?)assignment.Role).SingleOrDefaultAsync(cancellationToken);
}
