
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantQuickHttpRequest(Guid? ConversationId, uint? Version);
