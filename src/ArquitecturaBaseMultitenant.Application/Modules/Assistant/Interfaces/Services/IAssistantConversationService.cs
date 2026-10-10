using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantConversationService
{
    Task<Result<AssistantStateResponse>> StateAsync(CancellationToken cancellationToken);
    Task<Result> AcceptNoticeAsync(AssistantNoticeRequest request, CancellationToken cancellationToken);
    Task<Result<CursorResult<AssistantConversationSummaryResponse>>> ListAsync(AssistantConversationListRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantConversationSummaryResponse>> CreateAsync(CreateAssistantConversationRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantConversationResponse>> GetAsync(Guid id, AssistantMessageListRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateAsync(Guid id, UpdateAssistantConversationRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid id, uint? version, CancellationToken cancellationToken);
    Task<Result<AssistantTurnResultResponse>> SendAsync(Guid id, SendAssistantMessageRequest request, CancellationToken cancellationToken);
    Task<Result> StopAsync(Guid id, Guid turnId, StopAssistantTurnRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantQuickResponse>> QuickAsync(string key, AssistantQuickRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> FeedbackAsync(Guid id, Guid messageId, AssistantFeedbackRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantAttachmentSummaryResponse>> UploadAsync(Guid id, AssistantUploadRequest request, CancellationToken cancellationToken);
    Task<Result<AssistantFileResponse>> DownloadAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken);
    Task<Result> DeleteAttachmentAsync(Guid id, Guid attachmentId, uint? version, CancellationToken cancellationToken);
    Task<Result<AssistantFileResponse>> ExportAsync(Guid id, AssistantExportFormat? format, CancellationToken cancellationToken);
}
