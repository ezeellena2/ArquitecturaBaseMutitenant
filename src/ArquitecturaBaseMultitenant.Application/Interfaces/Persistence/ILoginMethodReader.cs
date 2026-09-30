using ArquitecturaBaseMultitenant.Application.Models.Identity;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta los métodos de ingreso de una cuenta para la pantalla de gestión sin devolver entidades de escritura.</summary>
public interface ILoginMethodReader
{
    Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
