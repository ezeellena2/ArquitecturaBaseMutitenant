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

    public void Add(PlatformWhatsAppTeam team) { context.RequireTransaction(); context.Set<PlatformWhatsAppTeam>().Add(team); }
}
