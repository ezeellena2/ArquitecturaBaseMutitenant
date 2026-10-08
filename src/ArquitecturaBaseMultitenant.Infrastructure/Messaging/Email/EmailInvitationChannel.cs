using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Encola el correo de invitación cifrado en la transacción de emisión.</summary>
internal sealed class EmailInvitationChannel(IEmailTemplateRenderer templates, IOutbox outbox) : IInvitationChannel
{
    public string Key => InvitationChannel.Email;
    public LoginMethodType SupportedMethodType => LoginMethodType.Email;
    public bool IsAvailable => true;

    public async Task EnqueueAsync(InvitationDeliveryDestination destination, InvitationNotice notice, CultureProfile culture,
        Guid? recipientUserId, CancellationToken cancellationToken) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            await templates.RenderInvitationAsync(destination.Value, notice, culture, cancellationToken)), recipientUserId);
}
