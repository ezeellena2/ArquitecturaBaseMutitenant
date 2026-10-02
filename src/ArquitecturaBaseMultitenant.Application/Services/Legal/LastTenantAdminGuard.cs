using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Impide dejar sin Dueño activo una organización abierta y serializa el conteo con cambios de asignación.</summary>
internal sealed class LastTenantAdminGuard(ITenantAdminReader admins, ITenantAdminLock adminLocks) : ILastTenantAdminGuard
{
    public async Task<Result> CheckCanLeaveAsync(Guid userId, CancellationToken ct)
    {
        var tenantIds = await admins.ListActiveTenantIdsForOwnerAsync(userId, ct);
        foreach (var tenantId in tenantIds)
            await adminLocks.LockAssignmentsAsync(tenantId, ct);

        var ownerCounts = new List<int>(tenantIds.Count);
        foreach (var tenantId in tenantIds)
            ownerCounts.Add(await admins.CountActiveOwnersAsync(tenantId, ct));

        return AccountDeletionPolicy.CheckLastTenantAdmin(ownerCounts) is { } blocked
            ? blocked
            : Result.Success();
    }
}
