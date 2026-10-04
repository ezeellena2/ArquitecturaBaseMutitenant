using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// La regla de la única Dueña (ADR 0035): cada organización no cerrada, incluidas las suspendidas, conserva al menos
/// otra persona Dueña activa.
/// </summary>
public interface ILastTenantAdminGuard
{
    Task<Result> CheckCanLeaveAsync(Guid userId, CancellationToken ct);

    /// <summary>Los nombres de las organizaciones no cerradas donde la cuenta es la única Dueña activa, sin bloquear.</summary>
    Task<IReadOnlyList<string>> ListSoleOwnedOrganizationsAsync(Guid userId, CancellationToken ct);
}
