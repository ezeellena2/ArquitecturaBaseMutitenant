namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantConversationSummaryResponse(Guid Id, string? Title, bool IsSaved, uint Version, DateTime CreatedAtUtc, DateTime LastActivityAtUtc);
