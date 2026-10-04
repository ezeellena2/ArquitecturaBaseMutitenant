using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Regla de ADR 0035 para la baja iniciada por Plataforma: el único Dueño activo de una organización no cerrada
/// no se da de baja, aunque su cuenta esté suspendida. La ficha la muestra y la baja la exige, con el mismo
/// lock que serializa los cambios de Dueños.
/// </summary>
internal sealed class PlatformSoleOwnerGuard(ITenantAdminReader admins, ITenantAdminLock locks, ITenantReader tenants)
{
    internal async Task<IReadOnlyList<string>> ListAsync(Guid accountId, CancellationToken ct)
    {
        var names = new List<string>();
        foreach (var tenantId in await admins.ListOwnedOpenTenantIdsAsync(accountId, ct))
            if (await admins.CountOtherActiveOwnersAsync(tenantId, accountId, ct) == 0
                && await tenants.FindByIdAsync(tenantId, ct) is { } tenant)
                names.Add(tenant.Name);
        return [.. names.Order(StringComparer.CurrentCulture)];
    }

    internal async Task<Result> CheckAsync(Guid accountId, CancellationToken ct)
    {
        var tenantIds = await admins.ListOwnedOpenTenantIdsAsync(accountId, ct);
        foreach (var tenantId in tenantIds) await locks.LockAssignmentsAsync(tenantId, ct);
        foreach (var tenantId in tenantIds)
            if (await admins.CountOtherActiveOwnersAsync(tenantId, accountId, ct) == 0) return AccountDeletionErrors.LastAdmin;
        return Result.Success();
    }
}
