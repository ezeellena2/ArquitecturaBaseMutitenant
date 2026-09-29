using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Revoca los tokens de la autorización activa al cerrar sesión.</summary>
public interface IConnectLogoutService
{
    Task<Result> RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken);
}
