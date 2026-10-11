using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Technical accounting projection. Names never come from retained conversation content.</summary>
public sealed record AssistantUsageReadPage(Money Cost, Money? PlatformKeyCost, Money? Budget,
    int TotalQueries, int PeopleCount, int? OrganizationCount, bool IsCurrentMonth,
    PagedResult<AssistantUsageReadRow> Rows);

public sealed record AssistantUsageReadRow(AssistantUsageRowKind Kind, Guid? OrganizationId,
    Guid? PersonId, DateOnly? Day, string? OrganizationName, bool CanShowPersonName,
    int Queries, long InputTokens, long OutputTokens, long CacheWriteTokens, long CacheReadTokens,
    Money Cost, int? DailyLimit, Money? MonthlyBudget, int? PeopleCount, AssistantMode? FundingMode);

public sealed record AssistantPersonDailyUsageRow(Guid UserId, int ConsumedQueries, int ReservedQueries);
