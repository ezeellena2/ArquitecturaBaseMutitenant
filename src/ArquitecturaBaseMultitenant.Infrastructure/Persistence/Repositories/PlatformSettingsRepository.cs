using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class PlatformSettingsRepository(ApplicationDbContext context) : IPlatformSettingsRepository
{
    public Task<PlatformSettings?> GetAsync(CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformSettings.SingleOrDefaultAsync(
            settings => settings.Id == PlatformSettings.SingletonId, cancellationToken);
    }

    public void Add(PlatformSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        context.RequireTransaction();
        context.PlatformSettings.Add(settings);
    }
}
