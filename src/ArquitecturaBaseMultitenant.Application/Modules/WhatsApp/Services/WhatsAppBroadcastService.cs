using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppBroadcastService(TimeProvider clock, ILogger<WhatsAppBroadcastService> logger) : IWhatsAppBroadcastService
{
    public Task<Result<WhatsAppBroadcastListResponse>> ListAsync(WhatsAppBroadcastListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastList", () => Task.FromResult(Result.Failure<WhatsAppBroadcastListResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastListTemplateOptions", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppTemplateOptionResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastListTags", () => Task.FromResult(Result.Failure<PagedResult<string>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBroadcastDetailResponse>> GetAsync(Guid id, WhatsAppBroadcastDetailRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastGet", () => Task.FromResult(Result.Failure<WhatsAppBroadcastDetailResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBroadcastEstimateResponse>> EstimateAsync(WhatsAppBroadcastEstimateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastEstimate", () => Task.FromResult(Result.Failure<WhatsAppBroadcastEstimateResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBroadcastResponse>> CreateAsync(CreateWhatsAppBroadcastRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastCreate", () => Task.FromResult(Result.Failure<WhatsAppBroadcastResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBroadcastResponse>> CancelAsync(Guid id, CancelWhatsAppBroadcastRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastCancel", () => Task.FromResult(Result.Failure<WhatsAppBroadcastResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppBroadcastResponse>> DuplicateAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppBroadcastDuplicate", () => Task.FromResult(Result.Failure<WhatsAppBroadcastResponse>(ContractErrors.NotImplemented)));
}
