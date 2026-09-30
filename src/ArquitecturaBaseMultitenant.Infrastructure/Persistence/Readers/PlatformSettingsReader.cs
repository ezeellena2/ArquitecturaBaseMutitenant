using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Settings;
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
        await cache.GetOrCreateInOwnScopeAsync<ApplicationDbContext, PlatformSettingsRow?>(
            CacheKeys.Platform("settings"), scopes, LoadAsync, CacheOptions, cancellationToken);

    private static Task<PlatformSettingsRow?> LoadAsync(
        ApplicationDbContext context, CancellationToken cancellationToken) =>
        context.PlatformSettings.AsNoTracking()
            .Where(settings => settings.Id == PlatformSettings.SingletonId)
            .Select(settings => new PlatformSettingsRow(settings.ConsumerSignup, settings.BusinessSignup,
                settings.MaxOwnedOrganizations, settings.AccountDeletionGraceDays))
            .SingleOrDefaultAsync(cancellationToken);
}
