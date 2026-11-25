using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Repositories;

internal sealed class AiKeyRingControlRepository(ApplicationDbContext db) : IAiKeyRingControlRepository
{
    public Task LockAsync(CancellationToken ct)
    {
        db.RequireTransaction();
        return db.AcquireAdvisoryLocksAsync([AssistantLockKeys.KeyRing], ct);
    }
    public async Task<IReadOnlyList<AiKeyRingControl>> ListAsync(CancellationToken ct) =>
        await db.Set<AiKeyRingControl>().AsNoTracking().ToListAsync(ct);
    public void Add(AiKeyRingControl control) { db.RequireTransaction(); db.Add(control); }
}
