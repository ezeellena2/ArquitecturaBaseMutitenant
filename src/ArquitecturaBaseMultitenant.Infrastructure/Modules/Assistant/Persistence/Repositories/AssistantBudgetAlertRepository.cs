using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AssistantBudgetAlertRepository(ApplicationDbContext db) : IAssistantBudgetAlertRepository
{
    public async Task<AssistantBudgetAlert?> FindAsync(Guid monthId, AssistantKeyFunding funding, int threshold, CancellationToken ct)
    {
        db.RequireTransaction();
        await db.AcquireAdvisoryLocksAsync([AssistantLockKeys.BudgetAlert(monthId)], ct);
        var row = db.Set<AssistantBudgetAlert>().Local.SingleOrDefault(row => row.MonthId == monthId && row.Funding == funding && row.Threshold == threshold)
            ?? await db.Set<AssistantBudgetAlert>().SingleOrDefaultAsync(row => row.MonthId == monthId && row.Funding == funding && row.Threshold == threshold, ct);
        if (row is not null && db.Entry(row).State == EntityState.Unchanged) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public async Task<IReadOnlyList<Guid>> ListPendingAsync(CancellationToken ct) =>
        await db.Set<AssistantBudgetAlert>().AsNoTracking().Where(row => row.EnqueuedAtUtc == null).OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync(ct);
    public async Task<AssistantBudgetAlert?> ClaimAsync(Guid id, CancellationToken ct)
    {
        db.RequireTransaction();
        var monthId = await db.Set<AssistantBudgetAlert>().AsNoTracking().Where(row => row.Id == id).Select(row => (Guid?)row.MonthId).SingleOrDefaultAsync(ct);
        if (monthId is null) return null;
        await db.AcquireAdvisoryLocksAsync([AssistantLockKeys.BudgetAlert(monthId.Value)], ct);
        return await db.Set<AssistantBudgetAlert>().FromSqlInterpolated($"SELECT * FROM platform.\"AssistantBudgetAlerts\" WHERE \"Id\" = {id} FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
    }
    public void Add(AssistantBudgetAlert alert) { db.RequireTransaction(); db.Add(alert); }
}
