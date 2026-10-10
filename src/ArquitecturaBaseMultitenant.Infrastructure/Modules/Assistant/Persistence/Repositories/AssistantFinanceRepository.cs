using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantFinanceRepository(ApplicationDbContext db, ITenantContext tenant) : IAssistantFinanceRepository
{
    public Task LockAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct)
    {
        binding.Validate();
        if (tenant.TenantId != binding.ScopeTenantId) throw new InvalidOperationException("Financial accounting requires the original server scope.");
        // Callers acquire Account → Legal before finance. This whole batch is always acquired together.
        var keys = new List<string> { AdvisoryLockKeys.Account(binding.UserId) + ":assistant:finance",
            "global:assistant:finance:operation:" + operationId.ToString("N") };
        if (binding.ScopeTenantId is { } id) keys.Add(AdvisoryLockKeys.For(id, "assistant:finance:budget", id));
        return db.AcquireAdvisoryLocksAsync(keys, ct);
    }
    public async Task<AssistantFinancialOperation?> GetOperationAsync(AssistantFinancialBinding binding, Guid operationId, CancellationToken ct)
    {
        var local = db.Set<AssistantFinancialOperation>().Local.SingleOrDefault(row => row.Id == operationId);
        var row = local ?? await db.Set<AssistantFinancialOperation>().SingleOrDefaultAsync(row => row.Id == operationId, ct);
        if (row is not null) await RefreshAsync(row, ct);
        if (row is not null && !row.IsBoundTo(binding)) throw new InvalidOperationException("A financial identity cannot be rebound.");
        return row;
    }
    public async Task<AssistantPlatformKeyDailyQuota?> GetPlatformQuotaAsync(Guid userId, DateOnly dayUtc, CancellationToken ct)
    {
        var row = db.Set<AssistantPlatformKeyDailyQuota>().Local.SingleOrDefault(row => row.UserId == userId && row.DayUtc == dayUtc)
            ?? await db.Set<AssistantPlatformKeyDailyQuota>().SingleOrDefaultAsync(row => row.UserId == userId && row.DayUtc == dayUtc, ct);
        if (row is not null) await RefreshAsync(row, ct);
        return row;
    }
    public async Task<AssistantOwnKeyDailyQuota?> GetOwnQuotaAsync(Guid userId, DateOnly dayUtc, CancellationToken ct)
    {
        var id = tenant.RequiredTenantId;
        var row = db.Set<AssistantOwnKeyDailyQuota>().Local.SingleOrDefault(row => row.UserId == userId && row.DayUtc == dayUtc && (row.TenantId == id || row.TenantId == Guid.Empty))
            ?? await db.Set<AssistantOwnKeyDailyQuota>().SingleOrDefaultAsync(row => row.UserId == userId && row.DayUtc == dayUtc && row.TenantId == id, ct);
        if (row is not null) await RefreshAsync(row, ct);
        return row;
    }
    public async Task<AssistantBudgetMonth?> GetLatestMonthAsync(Guid organizationId, DateTime nowUtc, CancellationToken ct)
    {
        db.RequireTransaction();
        var local = db.Set<AssistantBudgetMonth>().Local.Where(row => row.OrganizationId == organizationId && row.StartUtc <= nowUtc)
            .OrderByDescending(row => row.StartUtc).FirstOrDefault();
        var persisted = await db.Set<AssistantBudgetMonth>().Where(row => row.OrganizationId == organizationId && row.StartUtc <= nowUtc)
            .OrderByDescending(row => row.StartUtc).FirstOrDefaultAsync(ct);
        return local is not null && (persisted is null || local.StartUtc >= persisted.StartUtc) ? local : persisted;
    }
    public async Task<decimal> GetCommittedAndReservedAmountAsync(Guid monthId, AssistantKeyFunding funding, CancellationToken ct)
    {
        db.RequireTransaction();
        var operations = db.Set<AssistantFinancialOperation>().AsNoTracking().Where(row => row.BudgetMonthId == monthId && row.Funding == funding);
        var reserved = await operations.Where(row => row.State == AssistantFinancialState.Reserved || row.State == AssistantFinancialState.Invoked)
            .SumAsync(row => row.ReservedCost.Amount, ct);
        var completed = await (from call in db.Set<AssistantFinancialCall>().AsNoTracking()
            join operation in operations.Where(row => row.State == AssistantFinancialState.Completed) on call.OperationId equals operation.Id
            select call.Cost.Amount).SumAsync(ct);
        // Multiple admissions in one caller UOW cannot ignore an Added reservation.
        var staged = db.Set<AssistantFinancialOperation>().Local.Where(row => db.Entry(row).State == EntityState.Added
            && row.BudgetMonthId == monthId && row.Funding == funding && row.State is AssistantFinancialState.Reserved or AssistantFinancialState.Invoked)
            .Sum(row => row.ReservedCost.Amount);
        return reserved + completed + staged;
    }
    public async Task<IReadOnlyList<AssistantFinancialCall>> ListCallsAsync(Guid operationId, CancellationToken ct)
    {
        db.RequireTransaction();
        var persisted = await db.Set<AssistantFinancialCall>().Where(row => row.OperationId == operationId).OrderBy(row => row.Sequence).ToArrayAsync(ct);
        foreach (var row in persisted) await RefreshAsync(row, ct);
        return persisted.Concat(db.Set<AssistantFinancialCall>().Local.Where(row => row.OperationId == operationId && db.Entry(row).State == EntityState.Added))
            .DistinctBy(row => row.Id).OrderBy(row => row.Sequence).ToArray();
    }
    private async Task RefreshAsync(Entity entity, CancellationToken ct)
    {
        db.RequireTransaction();
        db.ChangeTracker.DetectChanges();
        if (db.Entry(entity).State == EntityState.Unchanged) await db.Entry(entity).ReloadAsync(ct);
    }
    private void Stage(Entity entity)
    {
        db.RequireTransaction();
        if (db.Entry(entity).State == EntityState.Detached) db.Add(entity);
    }
    public void Add(AssistantFinancialOperation operation) => Stage(operation);
    public void Add(AssistantPlatformKeyDailyQuota quota) => Stage(quota);
    public void Add(AssistantOwnKeyDailyQuota quota) => Stage(quota);
    public void Add(AssistantBudgetMonth month) => Stage(month);
    public void Add(AssistantFinancialCall call) => Stage(call);
    public void Add(AssistantUsageFact fact)
    {
        if (tenant.TenantId is null) throw new InvalidOperationException("A private financial fact requires its original scope.");
        Stage(fact);
    }
}
