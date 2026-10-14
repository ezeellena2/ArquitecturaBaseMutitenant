using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppInvitationChannel(IWhatsAppChannelSettings settings, IWhatsAppTemplateRenderer templates, IOutbox outbox) : IInvitationChannel
{
    internal const string ChannelKey = "whatsapp";
    public string Key => ChannelKey;
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public bool IsAvailable => settings.IsEnabled;
    public async Task EnqueueAsync(InvitationDeliveryDestination destination, InvitationNotice notice, CultureProfile culture,
        Guid? recipientUserId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || destination.Type != SupportedMethodType) return;
        outbox.Enqueue(Key, JsonSerializer.Serialize(await templates.RenderInvitationAsync(destination.Value, notice, culture, cancellationToken)), recipientUserId);
    }
}
