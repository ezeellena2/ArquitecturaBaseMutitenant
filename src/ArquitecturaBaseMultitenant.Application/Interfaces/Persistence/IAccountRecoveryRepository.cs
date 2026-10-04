using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Pedidos de «Recuperar mi cuenta», globales; se escriben dentro de la UoW del caso de uso.</summary>
public interface IAccountRecoveryRepository
{
    /// <summary>Si el código verificado ya registró un pedido: cada código registra uno solo.</summary>
    Task<bool> ExistsForLoginCodeAsync(Guid loginCodeId, CancellationToken cancellationToken);

    void Add(AccountRecoveryRequest request);

    /// <summary>El pedido con seguimiento, para resolverlo dentro de la UoW.</summary>
    Task<AccountRecoveryRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Los pedidos más viejos cuyo aviso a la cuenta todavía no se resolvió, con seguimiento y bloqueados para que
    /// otro trabajo en paralelo no los repita.
    /// </summary>
    Task<IReadOnlyList<AccountRecoveryRequest>> ListAwaitingAccountNoticeAsync(int limit,
        CancellationToken cancellationToken);
}
