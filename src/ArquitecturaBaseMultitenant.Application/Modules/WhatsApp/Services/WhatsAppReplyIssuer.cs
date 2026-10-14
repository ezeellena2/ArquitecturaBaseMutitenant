using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Emite la respuesta libre de una persona dentro de la transacción del servicio: un número desconectado deja el chat para
/// leer (D7), pasadas las 24 horas hace falta una plantilla, y sin medio de pago Meta entrega los de servicio solo dentro de
/// los 1.000 gratis del mes (D1). Encola por el número que recibió la conversación y devuelve el mensaje del outbox.
/// Una plantilla sale en cualquier momento, pero solo si está aprobada en la cuenta de ese número y con medio de pago; una
/// Marketing, solo a un contacto que acepta promociones y con su botón «No recibir más».
/// </summary>
internal sealed class WhatsAppReplyIssuer(IWhatsAppChannelRepository channels, ITimeZoneService zones, IOutbox outbox, IWhatsAppTemplateReader templates,
    IWhatsAppCustomerRepository customers, TimeProvider clock)
{
    internal async Task<Result<Guid>> IssueAsync(WhatsAppChatBase chat, string text, CancellationToken ct)
    {
        if (await channels.GetAsync(chat.ChannelId, ct) is not { IsDisconnected: false } channel) return WhatsAppErrors.ConversationReadOnly;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (chat.WindowOpenUntilUtc is not { } expiresAtUtc || expiresAtUtc <= nowUtc) return WhatsAppErrors.ConversationWindowClosed;
        if (channel.Status == WhatsAppNumberStatus.PaymentMissing)
        {
            var month = WhatsAppChannel.UsageMonth(zones.ConvertToLocal(nowUtc, channel.UsageTimeZoneId));
            if ((await channels.GetUsageAsync(channel.Id, month, ct))?.ServiceDelivered >= WhatsAppFeature.FreeServiceMessagesPerMonth)
                return WhatsAppErrors.ConversationPaymentRequired;
        }
        var message = new WhatsAppOutboundMessage(channel.PhoneNumberId, chat.WaId, string.Empty, string.Empty, [], Body: text,
            ReplyExpiresAtUtc: expiresAtUtc, ChannelId: channel.Id);
        return outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message));
    }

    internal async Task<Result<WhatsAppIssuedTemplateMessage>> IssueTemplateAsync(WhatsAppChatBase chat, bool platform, Guid templateId,
        IReadOnlyList<string> variables, CancellationToken ct)
    {
        if (await channels.GetAsync(chat.ChannelId, ct) is not { IsDisconnected: false } channel) return WhatsAppErrors.ConversationReadOnly;
        if (await templates.FindAsync(platform, templateId, ct) is not { } template
            || !await templates.IsApprovedInAsync(channel.WabaId, template.MetaName, template.MetaLanguage, ct)) return WhatsAppErrors.TemplateNotApproved;
        var marketing = template.Category == WhatsAppTemplateCategory.Marketing;
        if (marketing && (chat is not WhatsAppChat { CustomerId: { } customerId }
            || (await customers.GetAsync(customerId, null, ct))?.Consent != WhatsAppConsent.Accepted)) return WhatsAppErrors.TemplateNotApproved;
        if (variables.Count != template.Variables.Count) return WhatsAppTeamService.Invalid("variables", "WhatsApp.Validation.VariablesInvalid");
        // Las plantillas siempre exigen medio de pago (error 131042 de Meta).
        if (channel.Status == WhatsAppNumberStatus.PaymentMissing) return WhatsAppErrors.ConversationPaymentRequired;
        var message = new WhatsAppOutboundMessage(channel.PhoneNumberId, chat.WaId, template.MetaName, template.MetaLanguage, variables,
            ChannelId: channel.Id, QuickReplyPayload: marketing ? BotButtons.Unsubscribe : null);
        return new WhatsAppIssuedTemplateMessage(outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message)), template.Id,
            WhatsAppTemplateBase.Render(template.Body, variables));
    }
}

internal sealed record WhatsAppIssuedTemplateMessage(Guid OutboxMessageId, Guid TemplateId, string Text);
