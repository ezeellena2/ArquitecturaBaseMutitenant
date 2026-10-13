using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppConversationService(TimeProvider clock, ILogger<WhatsAppConversationService> logger) : IWhatsAppConversationService
{
    public Task<Result<WhatsAppConversationListResponse>> ListAsync(WhatsAppInbox? inbox, WhatsAppConversationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationList", () => Task.FromResult(Result.Failure<WhatsAppConversationListResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppConversationResponse>> GetAsync(WhatsAppInbox? inbox, Guid id, WhatsAppConversationMessagesRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationGet", () => Task.FromResult(Result.Failure<WhatsAppConversationResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppConversationRowResponse>> TakeAsync(WhatsAppInbox? inbox, Guid id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationTake", () => Task.FromResult(Result.Failure<WhatsAppConversationRowResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppChatMessageResponse>> SendAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppMessageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationSend", () => Task.FromResult(Result.Failure<WhatsAppChatMessageResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppChatMessageResponse>> AddNoteAsync(WhatsAppInbox? inbox, Guid id, AddWhatsAppNoteRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationAddNote", () => Task.FromResult(Result.Failure<WhatsAppChatMessageResponse>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppConversationRowResponse>> TransferAsync(WhatsAppInbox? inbox, Guid id, TransferWhatsAppConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationTransfer", () => Task.FromResult(Result.Failure<WhatsAppConversationRowResponse>(ContractErrors.NotImplemented)));

    public Task<Result<PagedResult<WhatsAppTemplateOptionResponse>>> ListTemplateOptionsAsync(WhatsAppInbox? inbox, Guid id, WhatsAppTemplateOptionListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationListTemplateOptions", () => Task.FromResult(Result.Failure<PagedResult<WhatsAppTemplateOptionResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<WhatsAppChatMessageResponse>> SendTemplateAsync(WhatsAppInbox? inbox, Guid id, SendWhatsAppTemplateRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "WhatsAppConversationSendTemplate", () => Task.FromResult(Result.Failure<WhatsAppChatMessageResponse>(ContractErrors.NotImplemented)));
}
