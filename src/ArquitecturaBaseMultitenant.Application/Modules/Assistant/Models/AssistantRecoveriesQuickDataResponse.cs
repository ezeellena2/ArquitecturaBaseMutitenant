namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantRecoveriesQuickDataResponse(IReadOnlyList<AssistantRecoveryRowResponse> Recoveries, int TotalCount, bool HasMore) : AssistantQuickDataResponse;
