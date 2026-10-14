using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class PlatformWhatsAppBotRepository(ApplicationDbContext context) : IPlatformWhatsAppBotRepository
{
    private const string Lock = "module:whatsapp:platform-bot";

    public async Task<PlatformWhatsAppBot?> GetAsync(uint? expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([Lock], ct);
        var row = await context.Set<PlatformWhatsAppBot>().SingleOrDefaultAsync(value => value.Id == PlatformWhatsAppBot.SingletonId, ct);
        if (row is null || expectedVersion is not { } version) return row;
        if (row.Version != version) throw new ConcurrencyConflictException();
        context.Entry(row).Property(value => value.Version).OriginalValue = version;
        return row;
    }

    public void Add(PlatformWhatsAppBot bot) { context.RequireTransaction(); context.Set<PlatformWhatsAppBot>().Add(bot); }
}
