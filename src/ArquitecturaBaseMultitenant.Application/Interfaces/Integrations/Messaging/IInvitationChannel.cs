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

    Task EnqueueAsync(InvitationDeliveryDestination destination, InvitationNotice notice, CultureProfile culture, Guid? recipientUserId,
        CancellationToken cancellationToken);
}
