using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Organizaciones activas donde la cuenta es el único Dueño activo. Es el mismo conteo que
/// <c>LastTenantAdminGuard</c> al pedir la baja, sin bloquear: Mi cuenta y la ficha de Plataforma lo muestran
/// antes de intentarla.
/// </summary>
internal sealed class SoleOwnerOrganizations(ITenantAdminReader admins, ITenantReader tenants)
{
    internal async Task<IReadOnlyList<string>> ListAsync(Guid userId, CancellationToken ct)
    {
        var names = new List<string>();
        foreach (var tenantId in await admins.ListActiveTenantIdsForOwnerAsync(userId, ct))
            if (await admins.CountActiveOwnersAsync(tenantId, ct) <= 1 && await tenants.FindByIdAsync(tenantId, ct) is { } tenant)
                names.Add(tenant.Name);
        return [.. names.Order(StringComparer.CurrentCulture)];
    }
}
