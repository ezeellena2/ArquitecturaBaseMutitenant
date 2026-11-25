using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
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
/// todo sale de una lectura directa. Las cuotas compartidas (E9) van en el sobre: el limitador las lee en cada pedido.
/// </summary>
internal sealed class PlatformSettingsReader(SharedCache cache, ApplicationDbContext context, IEmailBrandingDefaults defaults)
    : IPlatformSettingsReader
{
    private static readonly CacheEntry Entry =
        new(CacheKeys.Platform("settings"), [CacheTags.PlatformConfiguration], TimeSpan.FromMinutes(5));

    public async Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrLoadAsync(Entry, 0,
            static (services, _, ct) => LoadAsync(services.GetRequiredService<ApplicationDbContext>(), ct),
            cancellationToken);
        if (cached is null) return null;
        // Los plazos y el nombre de los correos (272 y 273) tampoco entran al sobre: se leen de la base en cada uso (270).
        var direct = await Singleton(context)
            .Select(settings => new
            {
                settings.Version, settings.SalesEmail, settings.SupportEmail, settings.LoginAuditRetentionMonths,
                settings.LoginCodeRetentionDays, settings.ClosedInvitationRetentionDays, settings.AppName, settings.SenderName,
                settings.EmailLogoUrl,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (direct?.Version == cached.Version)
            return cached.ToRow(direct.SalesEmail, direct.SupportEmail) with
            {
                LoginAuditRetentionMonths = direct.LoginAuditRetentionMonths,
                LoginCodeRetentionDays = direct.LoginCodeRetentionDays,
                ClosedInvitationRetentionDays = direct.ClosedInvitationRetentionDays,
                AppName = direct.AppName, SenderName = direct.SenderName, EmailLogoUrl = direct.EmailLogoUrl,
            };

        return await Singleton(context).Select(settings => new PlatformSettingsRow(settings.ConsumerSignup,
                settings.BusinessSignup, settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays,
                settings.Version, settings.SalesEmail, settings.SupportEmail, settings.RateLimits,
                settings.LoginAuditRetentionMonths, settings.LoginCodeRetentionDays, settings.ClosedInvitationRetentionDays,
                settings.AppName, settings.SenderName, settings.EmailLogoUrl))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IdentityRetentionPeriods> FindRetentionPeriodsAsync(CancellationToken cancellationToken) =>
        await Singleton(context)
            .Select(settings => new IdentityRetentionPeriods(settings.LoginAuditRetentionMonths,
                settings.LoginCodeRetentionDays, settings.ClosedInvitationRetentionDays))
            .SingleOrDefaultAsync(cancellationToken) ?? IdentityRetentionPeriods.Default;

    public async Task<PlatformBrandingRow> FindBrandingAsync(CancellationToken cancellationToken)
    {
        var saved = await Singleton(context)
            .Select(settings => new { settings.AppName, settings.SenderName, settings.EmailLogoUrl })
            .SingleOrDefaultAsync(cancellationToken);
        return new PlatformBrandingRow(saved?.AppName ?? defaults.AppName, saved?.SenderName ?? defaults.SenderName,
            PlatformSettings.EffectiveLogoUrl(saved?.EmailLogoUrl, defaults.LogoUrl));
    }

    public async Task<PlatformRateLimits?> FindRateLimitsAsync(CancellationToken cancellationToken) =>
        (await cache.GetOrLoadAsync(Entry, 0,
            static (services, _, ct) => LoadAsync(services.GetRequiredService<ApplicationDbContext>(), ct),
            cancellationToken))?.RateLimits;

    private static IQueryable<PlatformSettings> Singleton(ApplicationDbContext context) =>
        context.PlatformSettings.AsNoTracking().Where(settings => settings.Id == PlatformSettings.SingletonId);

    private static Task<CachedSettings?> LoadAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        Singleton(context).Select(settings => new CachedSettings(settings.ConsumerSignup, settings.BusinessSignup,
                settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays, settings.Version, settings.RateLimits))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Lo que guarda la caché: la configuración sin los correos de contacto.</summary>
    private sealed record CachedSettings(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
        int MaxOwnedOrganizations, int AccountDeletionGraceDays, uint Version, PlatformRateLimits RateLimits)
    {
        public PlatformSettingsRow ToRow(Domain.ValueObjects.Email? salesEmail, Domain.ValueObjects.Email? supportEmail) =>
            new(ConsumerSignup, BusinessSignup, MaxOwnedOrganizations, AccountDeletionGraceDays, Version,
                salesEmail, supportEmail, RateLimits);
    }
}
