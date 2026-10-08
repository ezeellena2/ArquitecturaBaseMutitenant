using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>Contrato temporal; T01 no acepta ni procesa mensajes del proveedor.</summary>
internal sealed class WhatsAppWebhookService(TimeProvider clock, ILogger<WhatsAppWebhookService> logger) : IWhatsAppWebhookService
{
    public Task<Result<string>> VerifyAsync(string? mode, string? verifyToken, string? challenge, CancellationToken ct) =>
        OperationLog.RunAsync<string>(logger, clock, "VerifyWebhook", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<Result<string>>(ContractErrors.NotImplemented);
        });

    public Task<Result> ReceiveAsync(WhatsAppWebhookRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ReceiveWebhook", () =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Failure(ContractErrors.NotImplemented));
        });
}
