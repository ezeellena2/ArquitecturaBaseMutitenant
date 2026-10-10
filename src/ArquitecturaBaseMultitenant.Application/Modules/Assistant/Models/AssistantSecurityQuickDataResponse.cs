namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantSecurityQuickDataResponse(IReadOnlyList<AssistantSecurityRowResponse> Events, string? NextCursor, bool HasMore) : AssistantQuickDataResponse;
