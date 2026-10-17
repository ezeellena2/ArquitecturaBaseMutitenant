using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppTeamRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppTeamRepository
{
    public async Task<IReadOnlyList<WhatsAppTeam>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        return await context.Set<WhatsAppTeam>().Where(value => value.TenantId == tenantId && ids.Contains(value.Id)).ToArrayAsync(ct);
    }

    public async Task<WhatsAppTeam?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var row = await Teams().SingleOrDefaultAsync(value => value.Id == id, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(WhatsAppTeam team) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppTeam>().Add(team); }
    public void Remove(WhatsAppTeam team) { context.RequireTransaction(); context.Set<WhatsAppTeam>().Remove(team); }

    private IQueryable<WhatsAppTeam> Teams()
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppTeam>().Where(value => value.TenantId == tenantId);
    }
}
