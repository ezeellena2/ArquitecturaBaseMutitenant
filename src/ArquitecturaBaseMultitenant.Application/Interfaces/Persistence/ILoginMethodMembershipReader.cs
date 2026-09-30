namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Comprueba solo el acceso activo de una cuenta al dueño de su método administrado.</summary>
public interface ILoginMethodMembershipReader
{
    Task<bool> IsActiveAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
