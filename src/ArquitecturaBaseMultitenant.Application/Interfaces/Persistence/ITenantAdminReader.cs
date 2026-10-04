namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee Dueños activos desde el índice global permitido de membresías.</summary>
public interface ITenantAdminReader
{
    Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Organizaciones no cerradas, incluidas las suspendidas, donde la cuenta es Dueño con membresía activa,
    /// cualquiera sea el estado de la cuenta: la regla de la única Dueña (ADR 0035).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListOwnedOpenTenantIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>Otros Dueños con membresía y cuenta activas en esa organización.</summary>
    Task<int> CountOtherActiveOwnersAsync(Guid tenantId, Guid userId, CancellationToken ct);
}
