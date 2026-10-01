using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using EmailAddress = ArquitecturaBaseMultitenant.Domain.ValueObjects.Email;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Encola el correo de invitación cifrado en la transacción de emisión.</summary>
internal sealed class EmailInvitationChannel(IEmailTemplateRenderer templates, IOutbox outbox) : IInvitationChannel
{
    public string Key => InvitationChannel.Email;

    public async Task EnqueueAsync(EmailAddress destination, InvitationNotice notice, CultureProfile culture,
        Guid? recipientUserId, CancellationToken cancellationToken) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            await templates.RenderInvitationAsync(destination.Value, notice, culture, cancellationToken)), recipientUserId);
}
