namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Revoca autorizaciones y tokens de OpenIddict dentro de la transacción del caso de uso.</summary>
public interface ITokenRevoker
{
    Task RevokeAuthorizationAsync(string authorizationId, CancellationToken cancellationToken);

    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken);

    Task RevokeAccessAsync(Guid userId, Domain.Users.Access access, CancellationToken cancellationToken);

    Task RevokeTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
