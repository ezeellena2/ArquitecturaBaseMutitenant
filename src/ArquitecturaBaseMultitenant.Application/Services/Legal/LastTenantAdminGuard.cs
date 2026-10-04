using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>
/// La regla de la única Dueña (ADR 0035): bloquea cada organización no cerrada, incluidas las suspendidas, donde la
/// cuenta es Dueña y no queda otra Dueña activa. La usan la baja que pide la persona, la que inicia Plataforma y las
/// pantallas que avisan antes (Mi cuenta y la ficha de Cuentas); quitar a una Dueña de una organización mira solo esa
/// organización. El conteo se serializa con los cambios de asignación mediante el lock compartido.
/// </summary>
internal sealed class LastTenantAdminGuard(ITenantAdminReader admins, ITenantAdminLock adminLocks,
    ITenantReader tenants) : ILastTenantAdminGuard
{
    public async Task<Result> CheckCanLeaveAsync(Guid userId, CancellationToken ct)
    {
        var tenantIds = await admins.ListOwnedOpenTenantIdsAsync(userId, ct);
        foreach (var tenantId in tenantIds)
            await adminLocks.LockAssignmentsAsync(tenantId, ct);

        foreach (var tenantId in tenantIds)
            if (await admins.CountOtherActiveOwnersAsync(tenantId, userId, ct) == 0)
                return AccountDeletionErrors.LastAdmin;
        return Result.Success();
    }

    public async Task<Result> CheckAnotherOwnerAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await adminLocks.LockAssignmentsAsync(tenantId, ct);
        return await admins.CountOtherActiveOwnersAsync(tenantId, userId, ct) == 0
            ? MemberErrors.LastTenantAdmin
            : Result.Success();
    }

    public async Task<IReadOnlyList<string>> ListSoleOwnedOrganizationsAsync(Guid userId, CancellationToken ct)
    {
        var names = new List<string>();
        foreach (var tenantId in await admins.ListOwnedOpenTenantIdsAsync(userId, ct))
            if (await admins.CountOtherActiveOwnersAsync(tenantId, userId, ct) == 0
                && await tenants.FindByIdAsync(tenantId, ct) is { } tenant)
                names.Add(tenant.Name);
        return [.. names.Order(StringComparer.CurrentCulture)];
    }
}
