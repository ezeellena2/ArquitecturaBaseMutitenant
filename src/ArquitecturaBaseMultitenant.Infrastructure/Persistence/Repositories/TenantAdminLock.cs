using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Comparte el lock de asignaciones Dueño entre baja de cuenta y cambios de roles.</summary>
internal sealed class TenantAdminLock(ApplicationDbContext context) : ITenantAdminLock
{
    public Task LockAssignmentsAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant id is required.", nameof(tenantId));
        return context.AcquireAdvisoryLocksAsync(
            [AdvisoryLockKeys.For(tenantId, "tenant-admin", "assignments")], ct);
    }
}
