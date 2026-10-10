namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantConversationListRequest(string? Cursor, int? Limit, bool? Saved);
