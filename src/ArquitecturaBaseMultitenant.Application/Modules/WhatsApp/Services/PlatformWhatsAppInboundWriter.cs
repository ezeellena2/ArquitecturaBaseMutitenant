using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Las consultas al número de Plataforma (ADR 0038): con las consultas prendidas cada entrada queda en su conversación y
/// la atiende el bot de Plataforma; apagadas, responde solo el bot de ingreso de la Etapa 8. Como en las organizaciones, una
/// entrada con la ventana de 24 horas cerrada empieza otra conversación.
/// </summary>
internal sealed class PlatformWhatsAppInboundWriter(IPlatformWhatsAppChatRepository chats, IPayloadProtector protector,
    PlatformWhatsAppBotWriter bot, TimeProvider clock) : IWhatsAppInboundConsumer
{
    public async Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
    {
        if (context.Channel is not { OrganizationId: null, ConsultationsEnabled: true } channel || context.Contact is not { } contact
            || context.Message.Direction != WhatsAppMessageDirection.Inbound) return;
        var message = context.Message;
        if (await chats.HasInboundAsync(message.Wamid, ct)) return;
        var waId = PhoneNumber.Create(contact.WaId).Value;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var occurredAtUtc = message.OccurredAtUtc > nowUtc ? nowUtc : message.OccurredAtUtc;
        var text = message.EncryptedText is { } cipher ? protector.Unprotect(cipher) : null;
        await chats.LockAsync(channel.Id, waId.Value, ct);
        var chat = await chats.GetAsync(channel.Id, waId.Value, ct);
        var newConversation = chat is null || !chat.IsWindowOpen(occurredAtUtc);
        if (chat is null)
        {
            chat = PlatformWhatsAppChat.Create(channel.Id, waId, nowUtc);
            chats.Add(chat);
        }
        else if (newConversation) chat.RestartWithBot();
        chats.Add(PlatformWhatsAppChatMessage.Inbound(chat.Id, message.Wamid, text,
            WhatsAppOrganizationInboundWriter.MediaKind(message.ContentType), occurredAtUtc));
        chat.RecordInbound(occurredAtUtc, text);
        await bot.RespondAsync(context, chat, newConversation, ct);
    }
}
