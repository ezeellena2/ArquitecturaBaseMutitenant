namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantAuditRowResponse(Guid Id, string ActionKey, string? ActorDisplayName, DateTime CreatedAtUtc);
