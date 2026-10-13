using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppTemplateService(TimeProvider clock, ILogger<WhatsAppTemplateService> logger) : IWhatsAppTemplateService
{
    public Task<Result<WhatsAppTemplateListResponse>> ListAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateList", () => Task.FromResult(Result.Failure<WhatsAppTemplateListResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTemplateResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateGet", () => Task.FromResult(Result.Failure<WhatsAppTemplateResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTemplateResponse>> CreateAsync(CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateCreate", () => Task.FromResult(Result.Failure<WhatsAppTemplateResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppTemplateResponse>> DuplicateAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateDuplicate", () => Task.FromResult(Result.Failure<WhatsAppTemplateResponse>(ContractErrors.NotImplemented)));

    public Task<Result> DeleteAsync(Guid id, DeleteWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppTemplateDelete", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));
}
