using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Persiste la autorización OpenID Connect que vincula sesión, cuenta, acceso, cliente y tenant para la emisión de tokens.</summary>
public interface IConnectAuthorizationStore
{
    Task<string> CreateAsync(Guid userId, Access access, Guid? tenantId,
        string sessionId, string clientId, IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken);
}
