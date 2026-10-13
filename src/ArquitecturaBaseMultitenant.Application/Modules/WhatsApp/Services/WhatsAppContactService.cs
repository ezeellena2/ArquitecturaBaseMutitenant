using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppContactService(TimeProvider clock, ILogger<WhatsAppContactService> logger) : IWhatsAppContactService
{
    public Task<Result<WhatsAppContactListResponse>> ListAsync(WhatsAppContactListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactList", () => Task.FromResult(Result.Failure<WhatsAppContactListResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppContactResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactGet", () => Task.FromResult(Result.Failure<WhatsAppContactResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppContactConversationResponse>>> ListConversationsAsync(Guid id, WhatsAppContactChildListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactListConversations", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppContactConversationResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppContactBroadcastResponse>>> ListBroadcastsAsync(Guid id, WhatsAppContactChildListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactListBroadcasts", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppContactBroadcastResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppContactResponse>> UpdateAsync(Guid id, UpdateWhatsAppContactRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactUpdate", () => Task.FromResult(Result.Failure<WhatsAppContactResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<string>>> ListTagsAsync(WhatsAppTagListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactListTags", () => Task.FromResult(Result.Failure<PagedResult<string>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppFileResponse>> GetImportSampleAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactGetImportSample", () => Task.FromResult(Result.Failure<WhatsAppFileResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppImportResponse>> ImportAsync(ImportWhatsAppContactsRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppContactImport", () => Task.FromResult(Result.Failure<WhatsAppImportResponse>(ContractErrors.NotImplemented)));
}
