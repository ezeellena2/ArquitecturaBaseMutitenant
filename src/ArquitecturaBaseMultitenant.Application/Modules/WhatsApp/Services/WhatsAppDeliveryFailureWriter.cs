using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// El motivo de un <c>status: failed</c> que informa Meta (B5), con el mismo código que un rechazo al enviar
/// (<see cref="WhatsAppSendFailures"/>): en el mensaje del chat y en el destinatario de la difusión que salieron por ese mensaje
/// de la cola. Lo del número de Plataforma se anota en la transacción del webhook; lo de una organización lo deja el webhook en
/// la cola durable de la entrada y se anota acá, dentro de su alcance, con los reintentos de esa cola.
/// </summary>
internal sealed class WhatsAppDeliveryFailureWriter(IWhatsAppChatRepository chats, IPlatformWhatsAppChatRepository platformChats,
    IWhatsAppBroadcastRepository broadcasts, TimeProvider clock)
{
    internal async Task RecordPlatformAsync(Guid outboxMessageId, string wamid, string failureReason, CancellationToken ct)
    {
        if (await platformChats.GetOutboundAsync(outboxMessageId, ct) is { } message)
            message.ApplyDelivery(WhatsAppChatMessageStatus.Failed, wamid, failureReason);
    }

    internal async Task RecordOrganizationAsync(WhatsAppMessage failure, CancellationToken ct)
    {
        if (!failure.IsDeliveryFailure || failure.OutboxMessageId is not { } outboxMessageId || failure.FailureReason is not { } reason)
            return;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (await chats.GetOutboundAsync(outboxMessageId, ct) is { } message)
            message.ApplyDelivery(WhatsAppChatMessageStatus.Failed, failure.Wamid, reason);
        if (await broadcasts.GetByOutboxMessageAsync(outboxMessageId, ct) is { } recipient)
            recipient.ApplyDeliveryFailure(reason, failure.OccurredAtUtc > nowUtc ? nowUtc : failure.OccurredAtUtc);
    }
}
