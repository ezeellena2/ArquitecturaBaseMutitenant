using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record OrganizationAssistantUsageHttpQuery(string? Month, AssistantUsageGrouping? GroupBy, int? Page, int? PageSize);
