using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Proyección interna exacta; los tokens long no modifican el DTO HTTP congelado.</summary>
public sealed record AssistantFinancialAggregateRow(Access Access, Guid? OrganizationId, AssistantKeyFunding Funding,
    long Queries, long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens,
    long PeopleCount, Money Cost, Money? MonthlyBudget, DateOnly? Month, string? TimeZoneId,
    DateTime? MonthStartUtc, DateTime? MonthEndUtc);
