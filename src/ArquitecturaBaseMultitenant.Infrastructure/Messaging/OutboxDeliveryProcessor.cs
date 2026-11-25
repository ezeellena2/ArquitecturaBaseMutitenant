using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// Orquesta commit del claim, transporte sin transacción y commit del receipt en scopes separados (ADR 0066):
/// <list type="bullet">
/// <item>si el apagado llega antes de llamar al transporte, la reserva vuelve a pendiente sin gastar el intento;</item>
/// <item>lo que falla antes de llamar al transporte (abrir el payload) vuelve a pendiente con su espera;</item>
/// <item>una excepción durante la llamada deja el mensaje «Sin confirmar»: pudo haber salido;</item>
/// <item>el apagado no corta un transporte iniciado: lo acota la espera propia del canal, que con la confirmación cabe en
/// el apagado del host (<see cref="OutboxOptionsValidator"/>).</item>
/// </list>
/// </summary>
internal sealed class OutboxDeliveryProcessor(IServiceScopeFactory scopes, IPayloadProtector protector,
    TimeProvider clock, IOptions<OutboxOptions> options, ILogger<OutboxDeliveryProcessor> logger)
{
    internal const string NotStartedCode = "Outbox.Delivery.NotStarted";
    internal const string TransportFailedCode = "Outbox.Transport.Failed";

    internal async Task<int> ProcessOnceAsync(IReadOnlyCollection<IChannelSender> senders, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        OutboxDispatchClaim? claim;
        await using (var scope = scopes.CreateAsyncScope())
            claim = (await scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>()
                .ClaimNextAsync(senders.Select(value => value.Key).ToArray(), ct)).Value;
        if (claim is null) return 0;
        if (ct.IsCancellationRequested)
        {
            // Se apagó entre la reserva y el envío: no salió nada, y le toca enseguida a otra copia.
            await SettleAsync((service, token) => service.ReleaseAsync(claim.MessageId, claim.LeaseId, token));
            ct.ThrowIfCancellationRequested();
        }
        OutboxDeliveryReceipt receipt;
        IChannelSender sender;
        string payload;
        try
        {
            sender = senders.Single(value => value.Key == claim.Channel);
            payload = protector.Unprotect(claim.EncryptedPayload);
        }
        catch (Exception exception)
        {
            Log(claim, exception);
            await CompleteAsync(claim, OutboxDeliveryReceipt.Rejected(NotStartedCode, retryable: true));
            return 1;
        }
        // Stop frena el siguiente claim; no cancela un transporte iniciado que pudo ser aceptado.
        using var delivery = new CancellationTokenSource(options.Value.DeliveryTimeoutFor(claim.Channel), clock);
        try
        {
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
            // El transporte ya empezó: no se sabe si salió, y repetirlo podría mandarlo dos veces.
            Log(claim, exception);
            receipt = OutboxDeliveryReceipt.Uncertain(TransportFailedCode);
        }
        await CompleteAsync(claim, receipt);
        return 1;
    }

    private Task CompleteAsync(OutboxDispatchClaim claim, OutboxDeliveryReceipt receipt) =>
        SettleAsync((service, token) => service.CompleteAsync(claim.MessageId, claim.LeaseId, receipt, token));

    /// <summary>Anotar el resultado tiene su propio plazo, independiente del apagado.</summary>
    private async Task SettleAsync(Func<IOutboxDispatchService, CancellationToken, Task> settle)
    {
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.CompletionTimeoutSeconds), clock);
        await using var scope = scopes.CreateAsyncScope();
        await settle(scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>(), completion.Token);
    }

    private void Log(OutboxDispatchClaim claim, Exception exception)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = claim.MessageId });
        ExceptionLog.Write(logger, nameof(OutboxDeliveryProcessor), exception);
    }
}
