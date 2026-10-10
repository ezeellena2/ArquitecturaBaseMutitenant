namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantOrganizationsQuickDataResponse(IReadOnlyList<AssistantOrganizationRowResponse> Organizations, int TotalCount, bool HasMore) : AssistantQuickDataResponse;
