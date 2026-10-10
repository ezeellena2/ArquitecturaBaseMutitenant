using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantConversationService(TimeProvider clock, ILogger<AssistantConversationService> logger) : IAssistantConversationService
{
    public Task<Result<AssistantStateResponse>> StateAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationState", () => Task.FromResult(Result.Failure<AssistantStateResponse>(ContractErrors.NotImplemented)));

    public Task<Result> AcceptNoticeAsync(AssistantNoticeRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationAcceptNotice", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<CursorResult<AssistantConversationSummaryResponse>>> ListAsync(AssistantConversationListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationList", () => Task.FromResult(Result.Failure<CursorResult<AssistantConversationSummaryResponse>>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantConversationSummaryResponse>> CreateAsync(CreateAssistantConversationRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationCreate", () => Task.FromResult(Result.Failure<AssistantConversationSummaryResponse>(ContractErrors.NotImplemented)));

    public Task<Result<AssistantConversationResponse>> GetAsync(Guid id, AssistantMessageListRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationGet", () => Task.FromResult(Result.Failure<AssistantConversationResponse>(ContractErrors.NotImplemented)));

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
        OperationLog.RunAsync(logger, clock, "AssistantConversationDownload", () => Task.FromResult(Result.Failure<AssistantFileResponse>(ContractErrors.NotImplemented)));

    public Task<Result> DeleteAttachmentAsync(Guid id, Guid attachmentId, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationDeleteAttachment", () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));

    public Task<Result<AssistantFileResponse>> ExportAsync(Guid id, AssistantExportFormat? format, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, clock, "AssistantConversationExport", () => Task.FromResult(Result.Failure<AssistantFileResponse>(ContractErrors.NotImplemented)));
}
