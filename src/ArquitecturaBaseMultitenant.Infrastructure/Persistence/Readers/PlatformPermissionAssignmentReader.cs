using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyección de la asignación global vigente; no cachea permisos revocables.</summary>
internal sealed class PlatformPermissionAssignmentReader(ApplicationDbContext context) : IPlatformPermissionAssignmentReader
{
    public async Task<IReadOnlyList<string>?> FindPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var assignment = await context.PlatformPermissionAssignments.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        return assignment is null ? null : [.. assignment.Permissions];
    }
}
