using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Modifica habilitaciones dentro del único límite del servicio.</summary>
internal sealed class TenantFeatureRepository(ApplicationDbContext context) : ITenantFeatureRepository
{
    public Task LockAsync(Guid tenantId, string key, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.TenantFeature(tenantId, key)], cancellationToken);

    public async Task<TenantFeature?> GetForUpdateAsync(Guid tenantId, string key, uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        // Plataforma edita solo su fila (Plan, Manual o Trial), nunca la de otro origen (ADR 0069); si hubiera varias, la más
        // reciente. Se eligen en memoria porque son a lo sumo una por origen y IsPlatformManaged es la única regla.
        var rows = await context.TenantFeatures.Where(row => row.TenantId == tenantId && row.FeatureKey == key)
            .ToListAsync(cancellationToken);
        var feature = rows.Where(row => row.Source.IsPlatformManaged())
            .OrderByDescending(row => row.ModifiedAtUtc ?? row.CreatedAtUtc).ThenBy(row => row.Source.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
        if (feature is not null) context.Entry(feature).Property(row => row.Version).OriginalValue = expectedVersion;
        return feature;
    }

    public void Add(TenantFeature feature)
    {
        context.RequireTransaction();
        context.TenantFeatures.Add(feature);
    }

    public void MarkUpdated(TenantFeature feature)
    {
        context.RequireTransaction();
        context.Entry(feature).State = EntityState.Modified;
    }
}
