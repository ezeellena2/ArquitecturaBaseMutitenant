namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantLinkQueryResponse(AssistantLinkStatus? Status, int? Page, int? PageSize, string? Search);
