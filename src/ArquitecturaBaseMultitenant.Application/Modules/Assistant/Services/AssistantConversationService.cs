using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Application.Common.Validation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantConversationService(IAssistantConversationReader reader, ICurrentUser user, IRequestValidator validator, TimeProvider clock, ILogger<AssistantConversationService> logger) : IAssistantConversationService
{
    public Task<Result<AssistantStateResponse>> StateAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationState", () => Task.FromResult(Result.Failure<AssistantStateResponse>(ContractErrors.NotImplemented)));

    public Task<Result> AcceptNoticeAsync(AssistantNoticeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationAcceptNotice", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<CursorResult<AssistantConversationSummaryResponse>>> ListAsync(AssistantConversationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<CursorResult<AssistantConversationSummaryResponse>>(logger, clock, "AssistantConversationList", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await reader.ListAsync(user.UserId ?? throw new InvalidOperationException("A session is required."), user.Access ?? throw new InvalidOperationException("An access is required."), request, cancellationToken);
        });

    public Task<Result<AssistantConversationSummaryResponse>> CreateAsync(CreateAssistantConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationCreate", () => Task.FromResult(Result.Failure<AssistantConversationSummaryResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantConversationResponse>> GetAsync(Guid id, AssistantMessageListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantConversationResponse>(logger, clock, "AssistantConversationGet", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var ownerId = user.UserId ?? throw new InvalidOperationException("A session is required.");
            var access = user.Access ?? throw new InvalidOperationException("An access is required.");
            var conversation = await reader.FindByIdAsync(ownerId, access, id, cancellationToken);
            if (conversation is null) return AssistantErrors.ConversationNotFound;
            // T05 supplies the authorized durable non-terminal turn reference when admission is implemented.
            return new AssistantConversationResponse(conversation, await reader.ListMessagesAsync(ownerId, access, id, request, cancellationToken), null);
        });

    public Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationUpdate", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));

    public Task<Result> DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationDelete", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<AssistantTurnResultResponse>> SendAsync(Guid id, SendAssistantMessageRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationSend", () => Task.FromResult(Result.Failure<AssistantTurnResultResponse>(ContractErrors.NotImplemented)));

    public Task<Result> StopAsync(Guid id, Guid turnId, StopAssistantTurnRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationStop", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<AssistantQuickResponse>> QuickAsync(string key, AssistantQuickRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationQuick", () => Task.FromResult(Result.Failure<AssistantQuickResponse>(ContractErrors.NotImplemented)));

    public Task<Result<VersionResponse>> FeedbackAsync(Guid id, Guid messageId, AssistantFeedbackRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationFeedback", () => Task.FromResult(Result.Failure<VersionResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantAttachmentSummaryResponse>> UploadAsync(Guid id, AssistantUploadRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationUpload", () => Task.FromResult(Result.Failure<AssistantAttachmentSummaryResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantFileResponse>> DownloadAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<AssistantFileResponse>(logger, clock, "AssistantConversationDownload", async () =>
            await reader.ExistsAttachmentAsync(user.UserId ?? throw new InvalidOperationException("A session is required."), user.Access ?? throw new InvalidOperationException("An access is required."), id, attachmentId, cancellationToken)
                ? ContractErrors.NotImplemented : AssistantErrors.AttachmentNotFound);

    public Task<Result> DeleteAttachmentAsync(Guid id, Guid attachmentId, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationDeleteAttachment", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<AssistantFileResponse>> ExportAsync(Guid id, AssistantExportFormat? format, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationExport", () => Task.FromResult(Result.Failure<AssistantFileResponse>(ContractErrors.NotImplemented)));
}
