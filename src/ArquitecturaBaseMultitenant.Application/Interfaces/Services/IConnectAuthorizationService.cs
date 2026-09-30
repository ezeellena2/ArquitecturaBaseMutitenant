using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Persiste la autorización OpenID Connect para una cuenta y acceso ya seleccionados por el flujo de conexión.</summary>
public interface IConnectAuthorizationService
{
    Task<Result<string>> CreateAsync(Guid userId, Access access, Guid? tenantId,
        string sessionId, string clientId, IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken);
}
