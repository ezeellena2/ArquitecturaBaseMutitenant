
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantModelListHttpQuery(int? Page, int? PageSize, string? Search, bool? Active);
