namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantStartEventResponse(Guid TurnId, Guid UserMessageId, Guid AssistantMessageId, uint ConversationVersion);
