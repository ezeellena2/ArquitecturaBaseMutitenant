using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantTurnUsageResponse(int InputTokens, int OutputTokens, int CacheWriteTokens, int CacheReadTokens, Money Cost, int? RemainingMessages, Money? MonthlyUsedCost);
