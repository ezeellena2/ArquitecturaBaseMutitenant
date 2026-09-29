using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class TenantSettingsReader(
    ITenantContext tenantContext,
    IServiceScopeFactory scopes,
    HybridCache cache) : ITenantSettingsReader
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return await cache.GetOrCreateInOwnScopeAsync<TenantSettingsLoader, Guid, TenantSettingsRow?>(
            CacheKeys.Tenant(tenantId, "settings"), scopes, tenantId,
            static (loader, id, ct) => loader.FindAsync(id, ct), CacheOptions, cancellationToken);
    }
}

/// <summary>Un scope propio evita cachear filas de una transacción todavía reversible.</summary>
internal sealed class TenantSettingsLoader(ApplicationDbContext context, ITenantScope tenantScope)
{
    public async Task<TenantSettingsRow?> FindAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = tenantScope.Enter(tenantId);
        return await context.TenantSettings.AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => new TenantSettingsRow(settings.TenantId, settings.DefaultCulture,
                settings.DefaultTimeZoneId, settings.DefaultCurrency))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
