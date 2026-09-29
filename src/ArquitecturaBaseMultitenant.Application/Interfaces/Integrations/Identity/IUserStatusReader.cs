using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Consulta global acotada al estado de una identidad, sin exponer ApplicationUser.</summary>
public interface IUserStatusReader
{
    Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken);
}
