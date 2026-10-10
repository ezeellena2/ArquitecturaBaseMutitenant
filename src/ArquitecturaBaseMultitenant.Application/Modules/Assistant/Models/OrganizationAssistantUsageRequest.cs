namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record OrganizationAssistantUsageRequest(string? Month, AssistantUsageGrouping? GroupBy, int? Page, int? PageSize);
