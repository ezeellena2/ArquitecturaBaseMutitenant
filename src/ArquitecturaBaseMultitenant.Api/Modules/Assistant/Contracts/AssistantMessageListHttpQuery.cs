
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantMessageListHttpQuery(string? Cursor, int? Limit);
