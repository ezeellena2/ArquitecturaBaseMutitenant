using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

internal sealed class WhatsAppRateReader(ApplicationDbContext context) : IWhatsAppRateReader
{
    public async Task<Money?> FindAsync(string countryCode, WhatsAppPricingCategory category, DateOnly date, CancellationToken ct) =>
        (await context.Set<WhatsAppRate>().AsNoTracking()
            .Where(rate => rate.CountryCode == countryCode && rate.Category == category && rate.EffectiveFrom <= date)
            .OrderByDescending(rate => rate.EffectiveFrom).FirstOrDefaultAsync(ct))?.Price;
}
