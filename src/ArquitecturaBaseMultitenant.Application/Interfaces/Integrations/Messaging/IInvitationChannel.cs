using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para invitaciones; su implementación usa el outbox.</summary>
public interface IInvitationChannel
{
    string Key { get; }
    LoginMethodType SupportedMethodType { get; }
    bool IsAvailable { get; }

    /// <summary>
    /// Devuelve el id del mensaje de la cola, que la invitación guarda para derivar su estado de entrega (B6), o null si no
    /// salió nada.
    /// </summary>
    Task<Guid?> EnqueueAsync(InvitationDeliveryDestination destination, InvitationNotice notice, CultureProfile culture, Guid? recipientUserId,
        CancellationToken cancellationToken);
}
