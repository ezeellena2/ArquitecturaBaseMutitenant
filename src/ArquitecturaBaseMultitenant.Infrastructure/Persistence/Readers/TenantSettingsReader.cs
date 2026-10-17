using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Lee las preferencias del tenant activo desde la caché compartida (ADR 0052, 5 min) o una proyección sin seguimiento.
/// La clave sale del contexto del pedido y la carga corre en otro scope, así nunca se cachean datos de una transacción
/// todavía reversible.
/// </summary>
internal sealed class TenantSettingsReader(ITenantContext tenantContext, SharedCache cache) : ITenantSettingsReader
{
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public Task<TenantSettingsRow?> FindCurrentAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return cache.GetOrLoadAsync(
            new CacheEntry(CacheKeys.Tenant(tenantId, "settings"), [CacheTags.Settings(tenantId)], Expiration),
            tenantId,
            static (services, id, ct) => services.GetRequiredService<TenantSettingsLoader>().FindAsync(id, ct),
            cancellationToken);
    }
}

/// <summary>Un scope propio evita cachear filas de una transacción todavía reversible.</summary>
internal sealed class TenantSettingsLoader(ApplicationDbContext context, ITenantScope tenantScope)
{
    public async Task<TenantSettingsRow?> FindAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = tenantScope.Enter(tenantId);
        return await (from settings in context.TenantSettings.AsNoTracking()
                      join tenant in context.Tenants.AsNoTracking() on settings.TenantId equals tenant.Id
                      where settings.TenantId == tenantId
                      select new TenantSettingsRow(settings.TenantId, settings.DefaultCulture,
                          settings.DefaultTimeZoneId, settings.DefaultCurrency, tenant.Name, settings.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
