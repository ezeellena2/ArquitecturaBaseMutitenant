using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

public interface IAssistantTurnService
{
    Task<Result<AssistantTurnExecution>> ClaimAsync(Guid turnId, Guid leaseId, CancellationToken ct);
    Task<Result> BeginCallAsync(Guid turnId, Guid leaseId, int sequence, CancellationToken ct);
    Task<Result<AssistantTitleCall?>> BeginTitleAsync(Guid turnId, Guid leaseId, CancellationToken ct);
    Task<Result> FinishTitleAsync(Guid turnId, Guid leaseId, uint conversationVersion, string? title, AssistantCallUsage? usage, CancellationToken ct);
    Task<Result> HeartbeatAsync(Guid turnId, Guid leaseId, CancellationToken ct);
    Task<Result> CheckpointAsync(Guid turnId, Guid leaseId, string? delta, AssistantCallUsage? usage, bool callEnded, CancellationToken ct);
    Task<Result> RecoverAsync(Guid turnId, Guid leaseId, CancellationToken ct);
    Task<Result> RecordToolAsync(Guid turnId, Guid leaseId, string key, AssistantToolState state, CancellationToken ct);
    Task<Result<AssistantToolExecution>> ExecuteToolAsync(Guid turnId, Guid leaseId, string key, JsonElement arguments, CancellationToken ct);
    Task<Result> CompleteAsync(Guid turnId, Guid leaseId, string status, string? code, CancellationToken ct);
    Task<Result<bool>> CheckExecutionAsync(Guid turnId, Guid leaseId, CancellationToken ct);
    Task<Result<AssistantTurnRead>> ReadAsync(Guid turnId, int afterSequence, CancellationToken ct);
    Task<Result<IdempotentReplayResponse>> ReplayAsync(IdempotentReference reference, CancellationToken ct);
}
