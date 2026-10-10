
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantConversationListHttpQuery(string? Cursor, int? Limit, bool? Saved);
