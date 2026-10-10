namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record PlatformAssistantUsageRequest(string? Month, int? Page, int? PageSize);
