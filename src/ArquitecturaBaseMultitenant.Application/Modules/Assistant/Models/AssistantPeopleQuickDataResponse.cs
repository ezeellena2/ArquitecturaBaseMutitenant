namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantPeopleQuickDataResponse(IReadOnlyList<AssistantPersonRowResponse> People, int TotalCount, bool HasMore) : AssistantQuickDataResponse;
