using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// La regla de la única Dueña (ADR 0035): cada organización no cerrada, incluidas las suspendidas, conserva al menos
/// otra persona Dueña activa.
/// </summary>
public interface ILastTenantAdminGuard
{
    Task<Result> CheckCanLeaveAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Quitarle a una Dueña el rol, la membresía o el acceso en una organización: bloquea las asignaciones de esa
    /// organización y exige otra Dueña activa en ella, sin mirar las demás organizaciones de la cuenta.
    /// </summary>
    Task<Result> CheckAnotherOwnerAsync(Guid tenantId, Guid userId, CancellationToken ct);

    /// <summary>Los nombres de las organizaciones no cerradas donde la cuenta es la única Dueña activa, sin bloquear.</summary>
    Task<IReadOnlyList<string>> ListSoleOwnedOrganizationsAsync(Guid userId, CancellationToken ct);
}
