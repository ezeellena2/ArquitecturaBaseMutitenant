namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantToolEventResponse(Guid TurnId, string ToolKey, AssistantToolState State, string LabelKey);
