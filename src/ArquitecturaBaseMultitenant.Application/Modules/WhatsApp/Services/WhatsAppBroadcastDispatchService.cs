using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Manda una tanda de la difusión que más espera, dentro del alcance de la organización. Respeta el tope de Meta por 24
/// horas (<c>whatsapp_business_manager_messaging_limit</c>): lo que no entra sale en otra vuelta. Al salir vuelve a mirar el
/// consentimiento: quien ya no acepta promociones no recibe. Sin un número que la mande (plantilla que dejó de estar
/// aprobada o sin medio de pago), sus destinatarios quedan como no enviados con el motivo.
/// </summary>
internal sealed class WhatsAppBroadcastDispatchService(IWhatsAppBroadcastReader reader, IWhatsAppBroadcastRepository broadcasts,
    IWhatsAppTemplateReader templates, WhatsAppBroadcastChannelResolver channels, IWhatsAppTemplateClient client, IWhatsAppTokenProtector protector,
    IWhatsAppContactReader contacts, IOutbox outbox, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<WhatsAppBroadcastDispatchService> logger)
    : IWhatsAppBroadcastDispatchService
{
    internal const int BatchSize = 500;

    public Task<Result<bool>> DispatchNextAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<bool>(logger, clock, "WhatsAppBroadcastDispatch", async () =>
        {
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            if (await reader.FindNextDueAsync(nowUtc, cancellationToken) is not { } due) return false;
            var template = await templates.FindAsync(false, due.TemplateId, cancellationToken);
            var channel = template is { Status: WhatsAppTemplateStatus.Approved } ? await channels.ResolveAsync(template, cancellationToken)
                : Result.Failure<WhatsAppSendingChannelRow>(WhatsAppErrors.TemplateNotApproved);
            var budget = BatchSize;
            if (channel.IsSuccess)
            {
                // Las consultas a Meta van antes de la transacción; si no responde, se prueba en la vuelta siguiente.
                if (protector.TryUnprotect(channel.Value.Id, channel.Value.EncryptedToken) is not { } token) return false;
                var limit = await client.GetMessagingLimitAsync(channel.Value.PhoneNumberId, token, cancellationToken);
                if (limit.IsFailure) return false;
                if (limit.Value is { } max) budget = Math.Min(BatchSize, max - await reader.CountContactsSentSinceAsync(nowUtc.AddHours(-24), cancellationToken));
                if (budget <= 0) return false;
            }
            return await unitOfWork.ExecuteInTransactionAsync<Result<bool>>(async ct =>
            {
                if (await broadcasts.GetForUpdateAsync(due.Id, null, ct) is not { } broadcast || !broadcast.IsDue(nowUtc)) return false;
                broadcast.Start(nowUtc);
                var pending = await broadcasts.ListPendingAsync(broadcast.Id, budget, ct);
                if (channel.IsFailure)
                    foreach (var recipient in pending) recipient.Fail(channel.Error.Code, nowUtc);
                else
                {
                    var consents = await contacts.ListConsentsAsync([.. pending.Select(recipient => recipient.CustomerId)], ct);
                    foreach (var recipient in pending)
                    {
                        if (!consents.TryGetValue(recipient.CustomerId, out var consent) || consent != WhatsAppConsent.Accepted)
                        {
                            recipient.MarkUnsubscribed(nowUtc);
                            continue;
                        }
                        var message = new WhatsAppOutboundMessage(channel.Value.PhoneNumberId, recipient.WaId, template!.MetaName, template.MetaLanguage,
                            broadcast.Variables, ChannelId: channel.Value.Id, QuickReplyPayload: BotButtons.Unsubscribe);
                        recipient.MarkSent(outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message)), nowUtc);
                    }
                }
                if (pending.Count < budget) broadcast.Finish(nowUtc);
                return true;
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
