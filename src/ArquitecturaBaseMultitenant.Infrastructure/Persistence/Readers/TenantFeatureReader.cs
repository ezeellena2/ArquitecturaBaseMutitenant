using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Lee habilitaciones globales sin seguimiento ni datos privados: una fila por origen (ADR 0069), ordenadas por clave y,
/// dentro de cada clave, de la más reciente a la más vieja por su fecha de cambio.
/// </summary>
internal sealed class TenantFeatureReader(ApplicationDbContext context) : ITenantFeatureReader
{
    public async Task<IReadOnlyList<TenantFeatureRow>> ListAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.TenantFeatures.AsNoTracking().Where(row => row.TenantId == tenantId)
            .OrderBy(row => row.FeatureKey).ThenByDescending(row => row.ModifiedAtUtc ?? row.CreatedAtUtc).ThenBy(row => row.Source)
            .Select(row => new TenantFeatureRow(row.FeatureKey, row.Enabled, row.Source, row.ExpiresAtUtc, row.Version, row.ExpiresOn))
            .ToArrayAsync(cancellationToken);
}
