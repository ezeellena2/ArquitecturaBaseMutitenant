using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Pedidos de «Recuperar mi cuenta», globales; se escriben dentro de la UoW del caso de uso.</summary>
public interface IAccountRecoveryRepository
{
    /// <summary>Si el código verificado ya registró un pedido: cada código registra uno solo.</summary>
    Task<bool> ExistsForLoginCodeAsync(Guid loginCodeId, CancellationToken cancellationToken);

    void Add(AccountRecoveryRequest request);
}
