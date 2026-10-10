namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantDeltaEventResponse(Guid TurnId, int Sequence, string Text);
