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

    public void Add(WhatsAppTeam team) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppTeam>().Add(team); }
}
