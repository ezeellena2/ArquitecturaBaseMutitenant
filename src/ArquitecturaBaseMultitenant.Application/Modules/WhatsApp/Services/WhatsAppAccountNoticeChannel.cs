using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppAccountNoticeChannel(IWhatsAppChannelSettings settings, IWhatsAppTemplateRenderer templates, IOutbox outbox) : IAccountNoticeChannel
{
    public string Key => "whatsapp";
    public LoginMethodType MethodType => LoginMethodType.Phone;
    public async Task EnqueueAsync(Guid? userId, string destination, AccountNotice notice, CultureProfile culture, CancellationToken cancellationToken)
    {
        if (!settings.IsEnabled || notice is AccountNotice.OperatorInvited) return;
        var message = await templates.RenderNoticeAsync(destination, notice, culture, cancellationToken);
        if (message is not null) outbox.Enqueue(Key, JsonSerializer.Serialize(message), userId);
    }
}
