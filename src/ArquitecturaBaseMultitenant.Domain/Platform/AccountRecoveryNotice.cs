using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>
/// Marca técnica global del aviso diferido, con la identidad del pedido como PK y FK. Se guarda junto con el
/// outbox, sin editar el pedido ni cambiar la versión que vio el operador. Sin cuenta también se registra.
/// </summary>
public sealed class AccountRecoveryNotice : Entity
{
    private AccountRecoveryNotice() { }

    private AccountRecoveryNotice(Guid requestId, DateTime resolvedAtUtc) : base(requestId)
    {
        ResolvedAtUtc = resolvedAtUtc;
    }

    public DateTime ResolvedAtUtc { get; private set; }

    public static AccountRecoveryNotice Complete(Guid requestId, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new AccountRecoveryNotice(requestId, nowUtc);
    }
}
