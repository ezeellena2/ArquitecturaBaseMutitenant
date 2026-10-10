namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantErrorEventResponse(Guid? TurnId, string Code, string TraceId, int? RetryAfter, AssistantFailedStatus Status);
