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
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Manda una tanda de la difusión que más espera, dentro del alcance de la organización. Respeta el tope de Meta por 24
/// horas (<c>whatsapp_business_manager_messaging_limit</c>): lo que no entra sale en otra vuelta. Al salir vuelve a mirar el
/// consentimiento: quien ya no acepta promociones no recibe. Sin un número que la mande (plantilla que dejó de estar
/// aprobada o sin medio de pago), sus destinatarios quedan como no enviados con el motivo. Con el módulo apagado no encola
/// nada y saca de la cola lo que todavía no salió (<see cref="PauseNextAsync"/>): la difusión queda en pausa, no cancelada.
/// </summary>
internal sealed class WhatsAppBroadcastDispatchService(IWhatsAppBroadcastReader reader, IWhatsAppBroadcastRepository broadcasts,
    WhatsAppBroadcastChannelResolver channels, IWhatsAppTemplateClient client, IWhatsAppTokenProtector protector,
    IWhatsAppContactReader contacts, IOutbox outbox, WhatsAppModulePolicy module, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<WhatsAppBroadcastDispatchService> logger)
    : IWhatsAppBroadcastDispatchService
{
    internal const int BatchSize = 500;

    public Task<Result<bool>> DispatchNextAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<bool>(logger, clock, "WhatsAppBroadcastDispatch", async () =>
        {
            if (!await module.IsCurrentEnabledAsync(cancellationToken)) return false;
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            if (await reader.FindNextDueAsync(nowUtc, cancellationToken) is not { } due) return false;
            var template = await channels.FindTemplateAsync(due.TemplateId, cancellationToken);
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
                        // Lleva la organización: la cola no lo manda mientras su módulo esté apagado.
                        var message = new WhatsAppOutboundMessage(channel.Value.PhoneNumberId, recipient.WaId, template!.MetaName, template.MetaLanguage,
                            broadcast.Variables, ChannelId: channel.Value.Id,
                            QuickReplyPayload: template.HasUnsubscribeButton ? BotButtons.Unsubscribe : null, BroadcastOrganizationId: broadcast.TenantId);
                        recipient.MarkSent(outbox.Enqueue("whatsapp", JsonSerializer.Serialize(message), options: message.QueueOptions(OutboxPriority.Notice)), nowUtc);
                    }
                }
                if (pending.Count < budget) broadcast.Finish(nowUtc);
                return true;
            }, CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result<bool>> PauseNextAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<bool>(logger, clock, "WhatsAppBroadcastPause", async () =>
        {
            if (await module.IsCurrentEnabledAsync(cancellationToken)) return false;
            if (await reader.FindNextQueuedAsync(cancellationToken) is not { } broadcastId) return false;
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            return await unitOfWork.ExecuteInTransactionAsync<Result<bool>>(async ct =>
            {
                // El lock de la difusión no deja que una tanda la dé por enviada mientras le devuelve destinatarios.
                if (await broadcasts.GetForUpdateAsync(broadcastId, null, ct) is not { } broadcast) return false;
                var queued = await broadcasts.ListQueuedAsync(broadcast.Id, BatchSize, ct);
                var withdrawn = await outbox.WithdrawAsync([.. queued.Select(recipient => recipient.OutboxMessageId!.Value)], ct);
                foreach (var recipient in queued)
                    if (withdrawn.Contains(recipient.OutboxMessageId!.Value)) recipient.ReturnToPending(nowUtc);
                if (withdrawn.Count > 0) broadcast.Reopen();
                // Lo que otra copia está reservando no sale (la cola lo posterga) y vuelve en la próxima vuelta.
                return withdrawn.Count > 0 && queued.Count == BatchSize;
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
}
