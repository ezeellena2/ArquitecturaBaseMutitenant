namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee Dueños activos desde el índice global permitido de membresías.</summary>
public interface ITenantAdminReader
{
    Task<IReadOnlyList<Guid>> ListActiveTenantIdsForOwnerAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct);

    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Organizaciones no cerradas donde la cuenta es Dueño con membresía activa, cualquiera sea el estado de la
    /// cuenta: la regla de la baja iniciada por Plataforma (ADR 0035).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListOwnedOpenTenantIdsAsync(Guid userId, CancellationToken ct);

    /// <summary>Otros Dueños con membresía y cuenta activas en esa organización.</summary>
    Task<int> CountOtherActiveOwnersAsync(Guid tenantId, Guid userId, CancellationToken ct);
}
