using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara la lectura y alta de preferencias del tenant activo dentro de una transacción. La organización se toma del alcance técnico, nunca del request.</summary>
internal sealed class TenantSettingsRepository(
    ApplicationDbContext context, ITenantContext tenantContext) : ITenantSettingsRepository
{
    public Task<TenantSettings?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return context.TenantSettings.SingleOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);
    }

    public async Task<TenantSettings?> GetCurrentAsync(uint expectedVersion, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        var settings = await context.TenantSettings.SingleOrDefaultAsync(
            row => row.TenantId == tenantId, cancellationToken);
        if (settings is not null)
            context.Entry(settings).Property(candidate => candidate.Version).OriginalValue = expectedVersion;
        return settings;
    }

    public void Add(TenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.TenantSettings.Add(settings);
    }

    public void MarkUpdated(TenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Entry(settings).State = EntityState.Modified;
    }
}
