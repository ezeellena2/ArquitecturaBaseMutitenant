using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantConversationRetentionRow(Guid Id, Guid OwnerUserId, Access Access);
public sealed record AssistantFileCleanupRow(Guid Id, string StorageKey);
