using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Orquesta commit del claim, transporte sin transacción y commit del receipt en scopes separados.</summary>
internal sealed partial class OutboxDeliveryProcessor(IServiceScopeFactory scopes, IPayloadProtector protector,
    TimeProvider clock, IOptions<OutboxOptions> options, ILogger<OutboxDeliveryProcessor> logger)
{
    internal async Task<int> ProcessOnceAsync(IReadOnlyCollection<IChannelSender> senders, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OutboxDispatchClaim? claim;
        await using (var scope = scopes.CreateAsyncScope())
            claim = (await scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>()
                .ClaimNextAsync(senders.Select(value => value.Key).ToArray(), ct)).Value;
        if (claim is null) return 0;
        ct.ThrowIfCancellationRequested();
        OutboxDeliveryReceipt receipt;
        // Stop frena el siguiente claim; no cancela un transporte iniciado que pudo ser aceptado.
        using var delivery = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.DeliveryTimeoutSeconds), clock);
        try
        {
            var payload = protector.Unprotect(claim.EncryptedPayload);
            var sender = senders.Single(value => value.Key == claim.Channel);
            if (sender is IChannelReceiptSender receiptSender)
                receipt = await receiptSender.SendWithReceiptAsync(payload, delivery.Token);
            else
            {
                await sender.SendAsync(payload, delivery.Token);
                receipt = OutboxDeliveryReceipt.Accepted();
            }
        }
        catch (Exception exception)
        {
            LogDeliveryFailed(logger, claim.MessageId, exception.GetType().Name);
            receipt = OutboxDeliveryReceipt.Rejected("Outbox.Transport.Failed", retryable: true);
        }
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.CompletionTimeoutSeconds), clock);
        await using (var scope = scopes.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>()
                .CompleteAsync(claim.MessageId, claim.LeaseId, receipt, completion.Token);
        return 1;
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} failed with {ExceptionType}")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, string exceptionType);
}
