using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantUsagePageResponse(string Month, Money Cost, Money? PlatformKeyCost, Money? Budget, int TotalQueries, int PeopleCount, PagedResult<AssistantUsageRowResponse> Rows, AssistantBudgetProgressResponse? BudgetProgress);
