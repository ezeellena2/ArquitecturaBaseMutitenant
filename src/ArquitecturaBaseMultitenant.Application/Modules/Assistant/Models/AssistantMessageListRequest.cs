namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantMessageListRequest(string? Cursor, int? Limit);
