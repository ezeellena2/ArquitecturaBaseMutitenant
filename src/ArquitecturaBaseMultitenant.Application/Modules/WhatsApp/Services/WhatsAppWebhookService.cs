using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;
using WhatsAppUniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.UniqueKeys;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppWebhookService(IWhatsAppSignatureValidator signatures, IWhatsAppWebhookReader reader,
    WhatsAppWebhookWriter writer, WhatsAppTemplateStateWriter templates, IWhatsAppInboundSignal signal, IWhatsAppWebhookReplay replay, IWhatsAppChannelCache channels, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<WhatsAppWebhookService> logger) : IWhatsAppWebhookService
{
    public Task<Result<string>> VerifyAsync(string? mode, string? verifyToken, string? challenge, CancellationToken ct) =>
        OperationLog.RunAsync<string>(logger, clock, "VerifyWebhook", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<Result<string>>(mode == "subscribe" && signatures.IsValidVerifyToken(verifyToken)
                && !string.IsNullOrWhiteSpace(challenge) && challenge.Length <= 500 ? challenge : WhatsAppErrors.InvalidSignature);
        });
    public Task<Result> ReceiveAsync(WhatsAppWebhookRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ReceiveWebhook", async () =>
        {
            if (!signatures.IsValidSignature(request.OriginalBody.Span, request.Signature)) return WhatsAppErrors.InvalidSignature;
            var batch = reader.Read(request.OriginalBody);
            if (batch.IsFailure) return batch.Error;
            // El número de Plataforma de la configuración tiene que existir antes de ubicar la entrada por su número.
            await channels.LoadAsync(ct);
            Result result;
            try
            {
                result = await unitOfWork.ExecuteInTransactionAsync(async c =>
                {
                    var written = await writer.WriteAsync(batch.Value, c);
                    if (written.IsFailure) return written;
                    // Estado y categoría de las plantillas (B6): por cuenta de WhatsApp, en el orden de las cuentas.
                    foreach (var account in batch.Value.TemplateUpdates.GroupBy(value => value.WabaId, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
                        await templates.ApplyAsync(account.Key, [.. account.Select(value => value.Update)], replaceAll: false, c);
                    return written;
                }, CommitPolicy.OnSuccess, ct);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName is WhatsAppUniqueKeys.MessageEvent or WhatsAppUniqueKeys.ContactNumber)
            { return await replay.ReplayAsync(request, ct); }
            if (result.IsSuccess && batch.Value.Messages.Count > 0) signal.Notify();
            // Meta informó que un número quedó sin medio de pago: ingreso y avisos de Plataforma lo ven enseguida.
            if (result.IsSuccess && batch.Value.Statuses.Any(value => value.ErrorCode == WhatsAppChannelUsageWriter.PaymentRequiredError))
                await channels.RefreshAsync(ct);
            return result;
        });
}
