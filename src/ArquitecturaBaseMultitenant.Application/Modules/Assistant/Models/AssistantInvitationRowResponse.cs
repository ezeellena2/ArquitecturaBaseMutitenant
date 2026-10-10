namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantInvitationRowResponse(Guid Id, string? DisplayName, string Status, DateTime ExpiresAtUtc);
