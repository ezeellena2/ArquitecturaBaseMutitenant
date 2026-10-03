using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Modifica habilitaciones dentro del único límite del servicio.</summary>
internal sealed class TenantFeatureRepository(ApplicationDbContext context) : ITenantFeatureRepository
{
    public async Task<TenantFeature?> GetForUpdateAsync(Guid tenantId, string key, uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var feature = await context.TenantFeatures.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.FeatureKey == key, cancellationToken);
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
