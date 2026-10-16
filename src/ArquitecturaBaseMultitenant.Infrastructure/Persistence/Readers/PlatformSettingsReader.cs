using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Lee la configuración global con la parte no sensible desde la caché compartida (ADR 0052, 5 min). Los correos de
/// contacto nunca entran al sobre: se leen de PostgreSQL y se unen por versión; si la fila cambió entre las dos lecturas,
/// todo sale de una lectura directa.
/// </summary>
internal sealed class PlatformSettingsReader(SharedCache cache, ApplicationDbContext context) : IPlatformSettingsReader
{
    private static readonly CacheEntry Entry =
        new(CacheKeys.Platform("settings"), [CacheTags.PlatformConfiguration], TimeSpan.FromMinutes(5));

    public async Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrLoadAsync(Entry, 0,
            static (services, _, ct) => LoadAsync(services.GetRequiredService<ApplicationDbContext>(), ct),
            cancellationToken);
        if (cached is null) return null;
        var contacts = await Singleton(context)
            .Select(settings => new { settings.Version, settings.SalesEmail, settings.SupportEmail })
            .SingleOrDefaultAsync(cancellationToken);
        if (contacts?.Version == cached.Version)
            return cached.ToRow(contacts.SalesEmail, contacts.SupportEmail);

        return await Singleton(context).Select(settings => new PlatformSettingsRow(settings.ConsumerSignup,
                settings.BusinessSignup, settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays,
                settings.Version, settings.SalesEmail, settings.SupportEmail))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<PlatformSettings> Singleton(ApplicationDbContext context) =>
        context.PlatformSettings.AsNoTracking().Where(settings => settings.Id == PlatformSettings.SingletonId);

    private static Task<CachedSettings?> LoadAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        Singleton(context).Select(settings => new CachedSettings(settings.ConsumerSignup, settings.BusinessSignup,
                settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays, settings.Version))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Lo que guarda la caché: la configuración sin los correos de contacto.</summary>
    private sealed record CachedSettings(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
        int MaxOwnedOrganizations, int AccountDeletionGraceDays, uint Version)
    {
        public PlatformSettingsRow ToRow(Domain.ValueObjects.Email? salesEmail, Domain.ValueObjects.Email? supportEmail) =>
            new(ConsumerSignup, BusinessSignup, MaxOwnedOrganizations, AccountDeletionGraceDays, Version,
                salesEmail, supportEmail);
    }
}
