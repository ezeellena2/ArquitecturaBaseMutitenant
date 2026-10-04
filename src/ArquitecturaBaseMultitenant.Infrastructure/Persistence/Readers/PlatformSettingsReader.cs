using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Lee la configuración global en una proyección cacheada. Llena el caché desde un scope propio para no compartir el DbContext de otra operación.</summary>
internal sealed class PlatformSettingsReader(
    IServiceScopeFactory scopes,
    HybridCache cache) : IPlatformSettingsReader
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task<PlatformSettingsRow?> FindAsync(CancellationToken cancellationToken) =>
        (await cache.GetOrCreateInOwnScopeAsync<ApplicationDbContext, CachedSettings?>(
            CacheKeys.Platform("settings"), scopes, LoadAsync, CacheOptions, cancellationToken))?.ToRow();

    private static async Task<CachedSettings?> LoadAsync(
        ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var settings = await context.PlatformSettings.AsNoTracking()
            .Where(settings => settings.Id == PlatformSettings.SingletonId)
            .Select(settings => new
            {
                settings.ConsumerSignup, settings.BusinessSignup, settings.MaxOwnedOrganizations,
                settings.AccountDeletionGraceDays, settings.Version, settings.SalesEmail, settings.SupportEmail,
            })
            .SingleOrDefaultAsync(cancellationToken);
        return settings is null ? null : new CachedSettings(settings.ConsumerSignup, settings.BusinessSignup,
            settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays, settings.Version,
            settings.SalesEmail?.Value, settings.SupportEmail?.Value);
    }

    /// <summary>Lo que guarda la caché: los correos como texto, porque su serializador no conoce el value object.</summary>
    private sealed record CachedSettings(ConsumerSignupMode ConsumerSignup, BusinessSignupMode BusinessSignup,
        int MaxOwnedOrganizations, int AccountDeletionGraceDays, uint Version, string? SalesAddress, string? SupportAddress)
    {
        public PlatformSettingsRow ToRow() => new(ConsumerSignup, BusinessSignup, MaxOwnedOrganizations,
            AccountDeletionGraceDays, Version, ToEmail(SalesAddress), ToEmail(SupportAddress));

        private static Email? ToEmail(string? address) => address is null ? null : Email.Create(address).Value;
    }
}
