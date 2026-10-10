namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantRecoveryRowResponse(Guid Id, string? DisplayName, string Status, DateTime CreatedAtUtc);
