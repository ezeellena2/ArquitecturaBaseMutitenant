using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppPhoneImportService(TimeProvider clock, ILogger<WhatsAppPhoneImportService> logger) : IWhatsAppPhoneImportService
{
    public Task<Result<WhatsAppPhoneImportPreviewResponse>> PreviewAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppPhoneImportPreview", () => Task.FromResult(Result.Failure<WhatsAppPhoneImportPreviewResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppPhoneImportResponse>> ImportAsync(WhatsAppPhoneImportRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppPhoneImportImport", () => Task.FromResult(Result.Failure<WhatsAppPhoneImportResponse>(ContractErrors.NotImplemented)));
}
