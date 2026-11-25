using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// Entrega un aviso a un método de ingreso mediante un canal registrado y el outbox. Sin cuenta cuando el destino
/// todavía no es de ninguna, como el método nuevo de «Recuperar mi cuenta».
/// </summary>
public interface IAccountNoticeChannel
{
    string Key { get; }
    LoginMethodType MethodType { get; }

    /// <summary>Devuelve el id del mensaje de la cola, o null si este canal no lo manda (B6).</summary>
    Task<Guid?> EnqueueAsync(Guid? userId, string destination, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken);
}
