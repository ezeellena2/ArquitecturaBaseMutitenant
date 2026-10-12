using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantAttachmentService
{
    Task<Result<IdempotentReplayResponse<AssistantAttachmentSummaryResponse>>> UploadAsync(Guid id, AssistantUploadRequest request, CancellationToken ct);
    Task<Result<AssistantFileResponse>> DownloadAsync(Guid id, Guid attachmentId, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, Guid attachmentId, uint? version, CancellationToken ct);
}
