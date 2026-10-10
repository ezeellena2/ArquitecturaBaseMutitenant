namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantQuickRequest(Guid? ConversationId, uint? Version);
