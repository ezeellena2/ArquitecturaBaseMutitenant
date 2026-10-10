namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantAuditQuickDataResponse(IReadOnlyList<AssistantAuditRowResponse> Changes, string? NextCursor, bool HasMore) : AssistantQuickDataResponse;
