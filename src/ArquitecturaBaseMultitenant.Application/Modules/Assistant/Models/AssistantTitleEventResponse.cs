namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantTitleEventResponse(Guid ConversationId, string Title, uint Version);
