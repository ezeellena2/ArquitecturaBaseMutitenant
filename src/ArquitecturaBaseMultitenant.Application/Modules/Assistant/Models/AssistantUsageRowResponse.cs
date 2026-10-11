using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantUsageRowResponse(AssistantUsageRowKind Kind, Guid? OrganizationId, Guid? PersonId, DateOnly? Day, string DisplayName, int Queries, long InputTokens, long OutputTokens, long CacheWriteTokens, long CacheReadTokens, Money Cost, int? DailyLimit, Money? MonthlyBudget, AssistantDailyUsageResponse? DailyUsage, int? PeopleCount, AssistantMode? FundingMode, AssistantBudgetProgressResponse? BudgetProgress);
