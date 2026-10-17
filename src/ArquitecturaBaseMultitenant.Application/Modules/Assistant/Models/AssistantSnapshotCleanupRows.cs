using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantSnapshotCleanupRow(bool IsAvailable, AssistantDurableOperationBase? Operation);
