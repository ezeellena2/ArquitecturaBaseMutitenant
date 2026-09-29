using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Consulta los accesos de una identidad global para selección y perfil.</summary>
public interface IUserTenantAccessReader
{
    Task<IReadOnlyList<UserTenantAccessRow>> ListForUserAsync(Guid userId,
        CancellationToken cancellationToken);
}
