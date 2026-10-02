namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Lee Dueños activos desde el índice global permitido de membresías.</summary>
public interface ITenantAdminReader
{
    Task<IReadOnlyList<Guid>> ListActiveTenantIdsForOwnerAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> ListOwnedBusinessTenantIdsAsync(Guid userId, CancellationToken ct);

    Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken ct);
}
