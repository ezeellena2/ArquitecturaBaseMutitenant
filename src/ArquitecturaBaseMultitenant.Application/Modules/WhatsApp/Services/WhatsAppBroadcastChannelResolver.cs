using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El número que manda una difusión: el primero conectado de la organización cuya cuenta de WhatsApp tiene la plantilla
/// aprobada. Si solo la tienen números sin medio de pago, PaymentRequired; si ninguno, NotApproved.
/// </summary>
internal sealed class WhatsAppBroadcastChannelResolver(IWhatsAppChannelReader channels, IWhatsAppTemplateReader templates)
{
    internal async Task<Result<WhatsAppSendingChannelRow>> ResolveAsync(WhatsAppTemplateRow template, CancellationToken ct)
    {
        if (template.Category != WhatsAppTemplateCategory.Marketing) return WhatsAppErrors.TemplateNotApproved;
        var paymentMissing = false;
        foreach (var account in await channels.ListTemplateAccountsAsync(platform: false, ct))
        {
            if (!await templates.IsApprovedInAsync(account.WabaId, template.MetaName, template.MetaLanguage, ct)
                || await channels.FindSendingAsync(account.ChannelId, string.Empty, ct) is not { } channel) continue;
            if (channel.Status == WhatsAppNumberStatus.Connected) return channel;
            paymentMissing |= channel.Status == WhatsAppNumberStatus.PaymentMissing;
        }
        return paymentMissing ? WhatsAppErrors.ConversationPaymentRequired : WhatsAppErrors.TemplateNotApproved;
    }
}
