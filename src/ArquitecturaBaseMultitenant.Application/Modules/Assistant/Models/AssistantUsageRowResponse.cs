using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantUsageRowResponse(AssistantUsageRowKind Kind, Guid? OrganizationId, Guid? PersonId, DateOnly? Day, string DisplayName, int Queries, int InputTokens, int OutputTokens, int CacheWriteTokens, int CacheReadTokens, Money Cost, int? DailyLimit, Money? MonthlyBudget);
