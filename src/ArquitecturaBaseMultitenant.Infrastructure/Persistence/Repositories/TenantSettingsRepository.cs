using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class TenantSettingsRepository(
    ApplicationDbContext context, ITenantContext tenantContext) : ITenantSettingsRepository
{
    public Task<TenantSettings?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var tenantId = tenantContext.RequiredTenantId;
        return context.TenantSettings.SingleOrDefaultAsync(
            settings => settings.TenantId == tenantId, cancellationToken);
    }

    public void Add(TenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.TenantSettings.Add(settings);
    }
}
