using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

internal sealed class AssistantFinanceReader(ApplicationDbContext db, ITenantContext tenant) : IAssistantFinanceReader
{
    public async Task<AssistantDailyQuotaUsageRow> FindDailyQuotaAsync(Guid userId, AssistantMode mode, DateOnly dayUtc, CancellationToken ct)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(mode)) throw new ArgumentException("A user and a valid funding mode are required.");
        if (mode == AssistantMode.PlatformKey)
            return await db.Set<AssistantPlatformKeyDailyQuota>().AsNoTracking()
                .Where(row => row.UserId == userId && row.DayUtc == dayUtc)
                .Select(row => new AssistantDailyQuotaUsageRow(row.ConsumedQueries, row.ReservedQueries))
                .SingleOrDefaultAsync(ct) ?? new(0, 0);
        var id = tenant.RequiredTenantId;
        return await db.Set<AssistantOwnKeyDailyQuota>().AsNoTracking()
            .Where(row => row.TenantId == id && row.UserId == userId && row.DayUtc == dayUtc)
            .Select(row => new AssistantDailyQuotaUsageRow(row.ConsumedQueries, row.ReservedQueries))
            .SingleOrDefaultAsync(ct) ?? new(0, 0);
    }

    public Task<AssistantBudgetMonthRow?> FindLatestMonthAsync(DateTime nowUtc, CancellationToken ct)
    {
        var id = tenant.RequiredTenantId;
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        // Return an expired last period too: the caller continues from its immutable end without writing.
        return db.Set<AssistantBudgetMonth>().AsNoTracking().Where(row => row.OrganizationId == id && row.StartUtc <= nowUtc)
            .OrderByDescending(row => row.StartUtc).ThenByDescending(row => row.Id)
            .Select(row => new AssistantBudgetMonthRow(row.Id, row.Month, row.TimeZoneId, row.StartUtc, row.EndUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AssistantMonthCostRow> FindMonthCostAsync(Guid monthId, AssistantKeyFunding funding, CancellationToken ct)
    {
        var id = tenant.RequiredTenantId;
        if (monthId == Guid.Empty || !Enum.IsDefined(funding)) throw new ArgumentException("A month and a valid funding are required.");
        var completed = db.Set<AssistantFinancialOperation>().AsNoTracking().Where(row => row.ScopeTenantId == id
            && row.Access == Access.Business && row.BudgetMonthId == monthId && row.Funding == funding
            && row.State == AssistantFinancialState.Completed);
        // The technical ledger survives content deletion; a private fact is deliberately not recreated then.
        // Budget and cost share one statement snapshot even when a newer admission completes concurrently.
        var snapshot = await completed.OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id)
            .Select(row => new
            {
                Budget = row.MonthlyBudget,
                Cost = (from call in db.Set<AssistantFinancialCall>().AsNoTracking()
                    join operation in completed on call.OperationId equals operation.Id
                    where operation.BudgetMonthId == row.BudgetMonthId && operation.Funding == row.Funding
                    select call.Cost.Amount).Sum()
            }).FirstOrDefaultAsync(ct);
        return snapshot?.Budget is { } budget ? new(budget, new Money(snapshot.Cost, budget.Currency)) : new(null, null);
    }

    public async Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListPlatformAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        if (tenant.TenantId is not null) throw new InvalidOperationException("Platform aggregates require a global management scope.");
        var operations = db.Set<AssistantFinancialOperation>().AsNoTracking().Where(row => row.State == AssistantFinancialState.Completed
            && row.CreatedAtUtc >= startUtc && row.CreatedAtUtc < endUtc);
        var aggregate = await (from call in db.Set<AssistantFinancialCall>().AsNoTracking()
            join operation in operations on call.OperationId equals operation.Id
            group new { call, operation } by new { operation.Access, OrganizationId = operation.Access == Access.Business ? operation.ScopeTenantId : null,
                operation.Funding, operation.BudgetMonthId, call.Cost.Currency } into rows
            select new
            {
                rows.Key.Access, rows.Key.OrganizationId, rows.Key.Funding, rows.Key.BudgetMonthId, rows.Key.Currency,
                Queries = rows.Select(row => row.operation.Id).Distinct().LongCount(),
                People = rows.Select(row => row.operation.UserId).Distinct().LongCount(),
                Input = rows.Sum(row => row.call.InputTokens), Output = rows.Sum(row => row.call.OutputTokens),
                Read = rows.Sum(row => row.call.CacheReadTokens),
                Write = rows.Sum(row => row.call.CacheWrite5MinutesTokens + row.call.CacheWrite1HourTokens + row.call.CacheWriteUnknownTokens),
                Cost = rows.Sum(row => row.call.Cost.Amount)
            }).ToArrayAsync(ct);
        var result = new List<AssistantFinancialAggregateRow>();
        foreach (var row in aggregate)
        {
            var month = row.BudgetMonthId is { } id ? await db.Set<AssistantBudgetMonth>().AsNoTracking()
                .Where(value => value.Id == id).Select(value => new { value.Month, value.TimeZoneId, value.StartUtc, value.EndUtc }).SingleAsync(ct) : null;
            var budget = row.BudgetMonthId is { } monthId ? await operations.Where(value => value.BudgetMonthId == monthId && value.Funding == row.Funding)
                .OrderByDescending(value => value.CreatedAtUtc).ThenByDescending(value => value.Id).Select(value => value.MonthlyBudget).FirstAsync(ct) : null;
            result.Add(new(row.Access, row.OrganizationId, row.Funding, row.Queries, row.Input, row.Output, row.Read, row.Write,
                row.People, new Money(row.Cost, row.Currency), budget, month?.Month, month?.TimeZoneId, month?.StartUtc, month?.EndUtc));
        }
        return result;
    }
    public async Task<IReadOnlyList<AssistantFinancialAggregateRow>> ListBusinessAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var id = tenant.RequiredTenantId;
        var scopedOperations = db.Set<AssistantFinancialOperation>().AsNoTracking().Where(row => row.ScopeTenantId == id
            && row.Access == Access.Business && row.State == AssistantFinancialState.Completed && row.CreatedAtUtc >= startUtc && row.CreatedAtUtc < endUtc);
        var facts = from fact in db.Set<AssistantUsageFact>().AsNoTracking().Where(row => row.TenantId == id && row.Access == Access.Business)
            join operation in scopedOperations on fact.OperationId equals operation.Id select fact;
        var rows = await facts.GroupBy(row => new { row.Funding, row.BudgetMonthId, row.Cost.Currency })
            .Select(group => new { group.Key.Funding, group.Key.Currency, group.Key.BudgetMonthId,
                Queries = group.Select(row => row.OperationId).Distinct().LongCount(), People = group.Select(row => row.UserId).Distinct().LongCount(),
                Input = group.Sum(row => row.InputTokens), Output = group.Sum(row => row.OutputTokens),
                Read = group.Sum(row => row.CacheReadTokens), Write = group.Sum(row => row.CacheWriteTokens), Cost = group.Sum(row => row.Cost.Amount) }).ToArrayAsync(ct);
        var result = new List<AssistantFinancialAggregateRow>();
        foreach (var row in rows)
        {
            var month = row.BudgetMonthId is { } monthId ? await db.Set<AssistantBudgetMonth>().AsNoTracking().Where(value => value.Id == monthId && value.OrganizationId == id)
                .Select(value => new { value.Month, value.TimeZoneId, value.StartUtc, value.EndUtc }).SingleAsync(ct) : null;
            var budget = row.BudgetMonthId is { } budgetId ? await scopedOperations
                .Where(value => value.BudgetMonthId == budgetId && value.Funding == row.Funding)
                .OrderByDescending(value => value.CreatedAtUtc).ThenByDescending(value => value.Id).Select(value => value.MonthlyBudget).FirstAsync(ct) : null;
            result.Add(new(Access.Business, id, row.Funding, row.Queries, row.Input, row.Output, row.Read, row.Write, row.People,
                new Money(row.Cost, row.Currency), budget, month?.Month, month?.TimeZoneId, month?.StartUtc, month?.EndUtc));
        }
        return result;
    }
}
