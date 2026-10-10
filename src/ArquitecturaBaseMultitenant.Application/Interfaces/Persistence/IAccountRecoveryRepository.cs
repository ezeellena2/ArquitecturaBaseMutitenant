using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Pedidos de «Recuperar mi cuenta», globales; se escriben dentro de la UoW del caso de uso.</summary>
public interface IAccountRecoveryRepository
{
    /// <summary>Si el código verificado ya registró un pedido: cada código registra uno solo.</summary>
    Task<bool> ExistsForLoginCodeAsync(Guid loginCodeId, CancellationToken cancellationToken);

    void Add(AccountRecoveryRequest request);

    /// <summary>Agrega la marca técnica del aviso bajo el lock del pedido y en la UoW que encoló el mensaje.</summary>
    void AddAccountNotice(AccountRecoveryNotice notice);

    /// <summary>Proyección sin seguimiento para tomar identidad/cuenta antes del lock de la solicitud.</summary>
    Task<AccountRecoveryIdentityHint?> GetIdentityHintAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>El pedido con seguimiento, para resolverlo dentro de la UoW.</summary>
    Task<AccountRecoveryRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Los pedidos más viejos cuyo aviso a la cuenta todavía no se resolvió, con seguimiento y bloqueados para que
    /// otro trabajo en paralelo no los repita.
    /// </summary>
    Task<IReadOnlyList<AccountRecoveryRequest>> ListAwaitingAccountNoticeAsync(int limit,
        CancellationToken cancellationToken);
}
