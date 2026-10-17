using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class PlatformWhatsAppTeamRepository(ApplicationDbContext context) : IPlatformWhatsAppTeamRepository
{
    public async Task<IReadOnlyList<PlatformWhatsAppTeam>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<PlatformWhatsAppTeam>().Where(value => ids.Contains(value.Id)).ToArrayAsync(ct);
    }

    public async Task<PlatformWhatsAppTeam?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var row = await context.Set<PlatformWhatsAppTeam>().SingleOrDefaultAsync(value => value.Id == id, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(PlatformWhatsAppTeam team) { context.RequireTransaction(); context.Set<PlatformWhatsAppTeam>().Add(team); }
    public void Remove(PlatformWhatsAppTeam team) { context.RequireTransaction(); context.Set<PlatformWhatsAppTeam>().Remove(team); }
}
