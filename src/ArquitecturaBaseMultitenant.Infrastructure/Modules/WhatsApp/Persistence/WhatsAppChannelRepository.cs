using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppChannelRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppChannelRepository
{
    private const string PlatformLock = "module:whatsapp:platform-channel";

    public Task LockNumberAsync(string phoneNumberId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumberId);
        return context.AcquireAdvisoryLocksAsync(["module:whatsapp:channel:" + phoneNumberId], ct);
    }

    public async Task<WhatsAppChannel?> GetOrganizationAsync(Guid id, uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var organizationId = tenant.RequiredTenantId;
        var row = await context.Set<WhatsAppChannel>().SingleOrDefaultAsync(value => value.Id == id
            && value.OrganizationId == organizationId && value.Status != WhatsAppNumberStatus.Disconnected, ct);
        return Expect(row, expectedVersion);
    }

    public async Task<WhatsAppChannel?> GetPlatformAsync(uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([PlatformLock], ct);
        var row = await context.Set<WhatsAppChannel>().SingleOrDefaultAsync(value => value.OrganizationId == null
            && value.Status != WhatsAppNumberStatus.Disconnected, ct);
        return Expect(row, expectedVersion);
    }

    public Task<WhatsAppChannel?> GetActiveAsync(string phoneNumberId, CancellationToken ct)
    {
        context.RequireTransaction();
        return context.Set<WhatsAppChannel>().SingleOrDefaultAsync(value => value.PhoneNumberId == phoneNumberId
            && value.Status != WhatsAppNumberStatus.Disconnected, ct);
    }

    public async Task<IReadOnlyList<WhatsAppChannel>> ListActiveByWabaAsync(string wabaId, CancellationToken ct)
    {
        context.RequireTransaction();
        return await context.Set<WhatsAppChannel>().Where(value => value.WabaId == wabaId && value.Status != WhatsAppNumberStatus.Disconnected)
            .OrderBy(value => value.Id).ToArrayAsync(ct);
    }

    public async Task<WhatsAppChannelUsage?> GetUsageAsync(Guid channelId, DateOnly month, CancellationToken ct)
    {
        context.RequireTransaction();
        // Un lote puede sumar varias entregas del mismo mes antes de guardar.
        return context.Set<WhatsAppChannelUsage>().Local.SingleOrDefault(value => value.ChannelId == channelId && value.Month == month)
            ?? await context.Set<WhatsAppChannelUsage>().SingleOrDefaultAsync(value => value.ChannelId == channelId && value.Month == month, ct);
    }

    public void Add(WhatsAppChannel channel) { context.RequireTransaction(); context.Set<WhatsAppChannel>().Add(channel); }
    public void Add(WhatsAppChannelUsage usage) { context.RequireTransaction(); context.Set<WhatsAppChannelUsage>().Add(usage); }
    public void MarkUpdated(WhatsAppChannel channel) { context.RequireTransaction(); context.Entry(channel).State = EntityState.Modified; }

    private WhatsAppChannel? Expect(WhatsAppChannel? row, uint? expectedVersion)
    {
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }
}
