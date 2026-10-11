using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

/// <summary>Reads only the technical ledger. The Business binding is mandatory even though the ledger is global.</summary>
internal sealed class AssistantUsageReader(ApplicationDbContext db, ITenantContext tenant) : IAssistantUsageReader
{
    public async Task<AssistantUsageReadPage> ListOrganizationAsync(DateOnly month, AssistantUsageGrouping grouping,
        int page, int pageSize, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var organizationId = tenant.RequiredTenantId;
        var periods = db.Set<AssistantBudgetMonth>().AsNoTracking()
            .Where(value => value.OrganizationId == organizationId && value.Month == month);
        var operations = Completed().Where(value => value.Access == Access.Business && value.ScopeTenantId == organizationId
            && periods.Any(period => period.Id == value.BudgetMonthId));
        var source = from operation in operations
            join period in periods on operation.BudgetMonthId equals period.Id
            join call in db.Set<AssistantFinancialCall>().AsNoTracking() on operation.Id equals call.OperationId
            select new UsageSource
            {
                OperationId = operation.Id, UserId = operation.UserId, Access = operation.Access,
                OrganizationId = operation.ScopeTenantId, Funding = operation.Funding,
                Day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(operation.CreatedAtUtc, period.TimeZoneId)),
                ContentDeleted = operation.ContentDeleted, DailyLimit = operation.DailyLimit,
                Input = call.InputTokens, Output = call.OutputTokens, Read = call.CacheReadTokens,
                Write = call.CacheWrite5MinutesTokens + call.CacheWrite1HourTokens + call.CacheWriteUnknownTokens,
                Cost = call.Cost.Amount
            };
        var summary = await SummaryAsync(source, operations, false, cancellationToken);
        var person = grouping == AssistantUsageGrouping.Person;
        var grouped = source.GroupBy(value => new
            {
                PersonId = person ? (Guid?)value.UserId : null,
                Day = person ? null : (DateOnly?)value.Day
            })
            .Select(rows => new UsageAggregate
            {
                PersonId = rows.Key.PersonId, Day = rows.Key.Day,
                CanShowPersonName = !rows.Any(value => value.ContentDeleted),
                Queries = rows.Select(value => value.OperationId).Distinct().Count(),
                People = rows.Select(value => value.UserId).Distinct().Count(),
                Input = rows.Sum(value => value.Input), Output = rows.Sum(value => value.Output),
                Read = rows.Sum(value => value.Read), Write = rows.Sum(value => value.Write),
                Cost = rows.Sum(value => value.Cost), DailyLimit = rows.Max(value => value.DailyLimit)
            });
        var paged = await grouped.OrderBy(value => value.Day).ThenBy(value => value.PersonId)
            .ToPagedResultAsync(new UsagePage { Page = page, PageSize = pageSize }, cancellationToken);
        var budget = summary?.FundingCount == 1 ? Budget(summary.Budget) : null;
        var items = paged.Items.Select(value => new AssistantUsageReadRow(person ? AssistantUsageRowKind.Person : AssistantUsageRowKind.Day,
            organizationId, value.PersonId, value.Day, null, value.CanShowPersonName,
            value.Queries, value.Input, value.Output, value.Write, value.Read, Amount(value.Cost),
            person ? value.DailyLimit : null, person ? budget : null, person ? null : value.People, null)).ToArray();
        var current = await periods.AnyAsync(value => value.StartUtc <= nowUtc && nowUtc < value.EndUtc, cancellationToken);
        return new(Amount(summary?.Cost ?? 0m), summary?.PlatformCount > 0 ? Amount(summary.PlatformCost) : null,
            budget, summary?.Queries ?? 0, summary?.People ?? 0, null, current,
            new(items, paged.Page, paged.PageSize, paged.TotalCount));
    }

    public async Task<AssistantUsageReadPage> ListPlatformAsync(DateOnly month, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not null) throw new InvalidOperationException("Global management scope is required.");
        var startUtc = month.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        // The last representable month has no following DateOnly; all persisted instants are bounded by DateTime.
        var lastMonth = month.Year == 9999 && month.Month == 12;
        var endUtc = lastMonth ? new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc)
            : month.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periods = db.Set<AssistantBudgetMonth>().AsNoTracking().Where(value => value.Month == month);
        var operations = Completed().Where(value => value.Access == Access.Business
            ? periods.Any(period => period.Id == value.BudgetMonthId && period.OrganizationId == value.ScopeTenantId)
            : value.CreatedAtUtc >= startUtc && (lastMonth || value.CreatedAtUtc < endUtc));
        var source = from operation in operations
            join call in db.Set<AssistantFinancialCall>().AsNoTracking() on operation.Id equals call.OperationId
            select new UsageSource
            {
                OperationId = operation.Id, UserId = operation.UserId, Access = operation.Access,
                OrganizationId = operation.Access == Access.Business ? operation.ScopeTenantId : null,
                Funding = operation.Funding, Input = call.InputTokens, Output = call.OutputTokens,
                Read = call.CacheReadTokens,
                Write = call.CacheWrite5MinutesTokens + call.CacheWrite1HourTokens + call.CacheWriteUnknownTokens,
                Cost = call.Cost.Amount
            };
        var summary = await SummaryAsync(source, operations, true, cancellationToken);
        var aggregate = source.GroupBy(value => new { value.Access, value.OrganizationId, value.Funding })
            .Select(rows => new UsageAggregate
            {
                Access = rows.Key.Access, OrganizationId = rows.Key.OrganizationId, Funding = rows.Key.Funding,
                Queries = rows.Select(value => value.OperationId).Distinct().Count(),
                Input = rows.Sum(value => value.Input), Output = rows.Sum(value => value.Output),
                Read = rows.Sum(value => value.Read), Write = rows.Sum(value => value.Write), Cost = rows.Sum(value => value.Cost),
                Budget = rows.Key.Access == Access.Business
                    ? operations.Where(value => value.Access == Access.Business && value.ScopeTenantId == rows.Key.OrganizationId
                        && value.Funding == rows.Key.Funding)
                        .OrderByDescending(value => value.CreatedAtUtc).ThenByDescending(value => value.Id)
                        .Select(value => (decimal?)value.MonthlyBudget!.Amount).FirstOrDefault() : null
            });
        var named = from row in aggregate
            join organization in db.Tenants.AsNoTracking() on row.OrganizationId equals organization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            select new { Row = row, Name = organization == null ? null : organization.Name };
        var paged = await named.OrderBy(value => value.Row.Access == Access.Business ? 0 : value.Row.Access == Access.Consumer ? 1 : 2)
            .ThenBy(value => value.Name).ThenBy(value => value.Row.OrganizationId).ThenBy(value => value.Row.Funding)
            .ToPagedResultAsync(new UsagePage { Page = page, PageSize = pageSize }, cancellationToken);
        var items = paged.Items.Select(value => new AssistantUsageReadRow(value.Row.Access == Access.Business ? AssistantUsageRowKind.Organization
                : value.Row.Access == Access.Consumer ? AssistantUsageRowKind.Personal : AssistantUsageRowKind.Operator,
            value.Row.OrganizationId, null, null, value.Name, false, value.Row.Queries,
            value.Row.Input, value.Row.Output, value.Row.Write, value.Row.Read, Amount(value.Row.Cost), null,
            Budget(value.Row.Budget), null, value.Row.Funding == AssistantKeyFunding.OwnKey ? AssistantMode.OwnKey : AssistantMode.PlatformKey)).ToArray();
        return new(Amount(summary?.Cost ?? 0m), Amount(summary?.PlatformCost ?? 0m), null,
            summary?.Queries ?? 0, summary?.People ?? 0, summary?.Organizations ?? 0, false,
            new(items, paged.Page, paged.PageSize, paged.TotalCount));
    }

    public async Task<IReadOnlyList<AssistantPersonDailyUsageRow>> ListDailyAsync(IReadOnlyCollection<Guid> userIds,
        AssistantMode mode, DateOnly dayUtc, CancellationToken cancellationToken)
    {
        if (mode == AssistantMode.PlatformKey)
            return await db.Set<AssistantPlatformKeyDailyQuota>().AsNoTracking().Where(value => userIds.Contains(value.UserId) && value.DayUtc == dayUtc)
                .Select(value => new AssistantPersonDailyUsageRow(value.UserId, value.ConsumedQueries, value.ReservedQueries)).ToArrayAsync(cancellationToken);
        var organizationId = tenant.RequiredTenantId;
        return await db.Set<AssistantOwnKeyDailyQuota>().AsNoTracking()
            .Where(value => value.TenantId == organizationId && userIds.Contains(value.UserId) && value.DayUtc == dayUtc)
            .Select(value => new AssistantPersonDailyUsageRow(value.UserId, value.ConsumedQueries, value.ReservedQueries)).ToArrayAsync(cancellationToken);
    }

    private IQueryable<AssistantFinancialOperation> Completed() => db.Set<AssistantFinancialOperation>().AsNoTracking()
        .Where(value => value.State == AssistantFinancialState.Completed);

    private static Task<UsageSummary?> SummaryAsync(IQueryable<UsageSource> source,
        IQueryable<AssistantFinancialOperation> operations, bool platform, CancellationToken cancellationToken) =>
        source.GroupBy(value => 1).Select(rows => new UsageSummary
        {
            Queries = rows.Select(value => value.OperationId).Distinct().Count(),
            People = rows.Select(value => value.UserId).Distinct().Count(),
            Organizations = rows.Where(value => value.OrganizationId != null).Select(value => value.OrganizationId).Distinct().Count(),
            FundingCount = rows.Select(value => value.Funding).Distinct().Count(),
            PlatformCount = rows.Count(value => value.Funding == AssistantKeyFunding.PlatformKey),
            PlatformCost = rows.Sum(value => value.Funding == AssistantKeyFunding.PlatformKey ? value.Cost : 0m),
            Cost = rows.Sum(value => value.Cost),
            // One SQL statement reads the matching admitted budget and its persisted cost.
            Budget = platform ? null : operations.OrderByDescending(value => value.CreatedAtUtc).ThenByDescending(value => value.Id)
                .Select(value => (decimal?)value.MonthlyBudget!.Amount).FirstOrDefault()
        }).SingleOrDefaultAsync(cancellationToken);

    private static Money Amount(decimal value) => new(value, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value);
    private static Money? Budget(decimal? value) => value is { } amount ? Amount(amount) : null;
    private sealed record UsagePage : PagedRequest;
    private sealed class UsageSource
    {
        public Guid OperationId { get; init; }
        public Guid UserId { get; init; }
        public Access Access { get; init; }
        public Guid? OrganizationId { get; init; }
        public AssistantKeyFunding Funding { get; init; }
        public DateOnly Day { get; init; }
        public bool ContentDeleted { get; init; }
        public int DailyLimit { get; init; }
        public long Input { get; init; }
        public long Output { get; init; }
        public long Read { get; init; }
        public long Write { get; init; }
        public decimal Cost { get; init; }
    }
    private sealed class UsageAggregate
    {
        public Access Access { get; init; }
        public Guid? OrganizationId { get; init; }
        public AssistantKeyFunding Funding { get; init; }
        public Guid? PersonId { get; init; }
        public DateOnly? Day { get; init; }
        public bool CanShowPersonName { get; init; }
        public int Queries { get; init; }
        public int People { get; init; }
        public long Input { get; init; }
        public long Output { get; init; }
        public long Read { get; init; }
        public long Write { get; init; }
        public decimal Cost { get; init; }
        public int DailyLimit { get; init; }
        public decimal? Budget { get; init; }
    }
    private sealed class UsageSummary
    {
        public int Queries { get; init; }
        public int People { get; init; }
        public int Organizations { get; init; }
        public int FundingCount { get; init; }
        public int PlatformCount { get; init; }
        public decimal PlatformCost { get; init; }
        public decimal Cost { get; init; }
        public decimal? Budget { get; init; }
    }
}
