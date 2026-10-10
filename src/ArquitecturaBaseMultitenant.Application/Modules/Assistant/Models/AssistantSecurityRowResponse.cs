namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantSecurityRowResponse(Guid Id, string EventKey, string? ActorDisplayName, DateTime CreatedAtUtc);
