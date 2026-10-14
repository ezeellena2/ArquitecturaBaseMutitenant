using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppTemplateRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppTemplateRepository
{
    public async Task<WhatsAppTemplate?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        var row = await context.Set<WhatsAppTemplate>().SingleOrDefaultAsync(value => value.TenantId == tenantId && value.Id == id, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(WhatsAppTemplate template) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Set<WhatsAppTemplate>().Add(template); }
    public void Remove(WhatsAppTemplate template) { context.RequireTransaction(); context.Set<WhatsAppTemplate>().Remove(template); }
}

internal sealed class PlatformWhatsAppTemplateRepository(ApplicationDbContext context) : IPlatformWhatsAppTemplateRepository
{
    public async Task<PlatformWhatsAppTemplate?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var row = await context.Set<PlatformWhatsAppTemplate>().SingleOrDefaultAsync(value => value.Id == id, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(PlatformWhatsAppTemplate template) { context.RequireTransaction(); context.Set<PlatformWhatsAppTemplate>().Add(template); }
    public void Remove(PlatformWhatsAppTemplate template) { context.RequireTransaction(); context.Set<PlatformWhatsAppTemplate>().Remove(template); }
}

internal sealed class WhatsAppTemplateStateRepository(ApplicationDbContext context) : IWhatsAppTemplateStateRepository
{
    public Task LockAsync(string wabaId, CancellationToken ct) => context.AcquireAdvisoryLocksAsync(["module:whatsapp:templates:" + wabaId], ct);

    public async Task<IReadOnlyList<WhatsAppTemplateState>> ListAsync(string wabaId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppTemplateState>().Where(value => value.WabaId == wabaId).ToArrayAsync(ct);
    }

    public void Add(WhatsAppTemplateState state) { context.RequireTransaction(); context.Set<WhatsAppTemplateState>().Add(state); }
    public void Remove(WhatsAppTemplateState state) { context.RequireTransaction(); context.Set<WhatsAppTemplateState>().Remove(state); }
}
