using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Revoca los tokens de la sesión del navegador al cerrar sesión.</summary>
public interface IConnectLogoutService
{
    Task<Result> RevokeAsync(Guid? userId, string? sessionId,
        string? authorizationId, CancellationToken cancellationToken);
}
