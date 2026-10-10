namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantTurnResultResponse(Guid TurnId, AssistantMessageResponse Message, uint ConversationVersion, AssistantTurnUsageResponse Usage);
