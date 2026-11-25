using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppInvitationChannel(IWhatsAppChannelSettings settings, IWhatsAppTemplateRenderer templates, IOutbox outbox) : IInvitationChannel
{
    internal const string ChannelKey = "whatsapp";
    public string Key => ChannelKey;
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public bool IsAvailable => settings.IsEnabled;
    public async Task<Guid?> EnqueueAsync(InvitationDeliveryDestination destination, InvitationNotice notice, CultureProfile culture,
        Guid? recipientUserId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || destination.Type != SupportedMethodType) return null;
        var message = await templates.RenderInvitationAsync(destination.Value, notice, culture, cancellationToken);
        return outbox.Enqueue(Key, JsonSerializer.Serialize(message), recipientUserId, message.QueueOptions(OutboxPriority.Notice));
    }
}
