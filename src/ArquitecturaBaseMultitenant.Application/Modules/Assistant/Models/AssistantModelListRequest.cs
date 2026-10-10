namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantModelListRequest(int? Page, int? PageSize, string? Search, bool? Active);
