using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Comprueba que la baja conserve al menos una persona Dueña activa en cada organización abierta.</summary>
public interface ILastTenantAdminGuard
{
    Task<Result> CheckCanLeaveAsync(Guid userId, CancellationToken ct);
}
