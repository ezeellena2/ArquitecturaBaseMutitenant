using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>
/// La configuración de WhatsApp para guardarla (B18), como la del asistente: lock de su fila, la versión que leyó la pantalla
/// (0 sin fila) y el 409 si no coincide. La de la organización sale del token y la filtra además RLS.
/// </summary>
internal sealed class WhatsAppSettingsRepository(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppSettingsRepository
{
    private const string PlatformLock = "global:whatsapp-settings";

    public async Task<WhatsAppPlatformSettings?> GetPlatformForUpdateAsync(uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync([PlatformLock], ct);
        var row = await context.Set<WhatsAppPlatformSettings>().SingleOrDefaultAsync(value => value.Id == WhatsAppPlatformSettings.SingletonId, ct);
        Check(row?.Version, expectedVersion);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }

    public async Task<WhatsAppOrganizationSettings?> GetOrganizationForUpdateAsync(uint expectedVersion, CancellationToken ct)
    {
        context.RequireTransaction();
        var tenantId = tenant.RequiredTenantId;
        await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.For(tenantId, "whatsapp-settings", "singleton")], ct);
        var row = await context.Set<WhatsAppOrganizationSettings>().SingleOrDefaultAsync(value => value.TenantId == tenantId, ct);
        Check(row?.Version, expectedVersion);
        if (row is not null) context.Entry(row).Property(value => value.Version).OriginalValue = expectedVersion;
        return row;
    }

    public void Add(WhatsAppPlatformSettings settings) { context.RequireTransaction(); context.Add(settings); }
    public void Add(WhatsAppOrganizationSettings settings) { context.RequireTransaction(); _ = tenant.RequiredTenantId; context.Add(settings); }
    public void MarkUpdated(WhatsAppPlatformSettings settings) { context.RequireTransaction(); context.Entry(settings).State = EntityState.Modified; }

    public void MarkUpdated(WhatsAppOrganizationSettings settings)
    {
        context.RequireTransaction();
        _ = tenant.RequiredTenantId;
        context.Entry(settings).State = EntityState.Modified;
    }

    private static void Check(uint? actual, uint expected)
    {
        if ((actual ?? 0) != expected) throw new ConcurrencyConflictException();
    }
}

/// <summary>La configuración de WhatsApp para leerla en cada uso (270), sin caché: una fila por clave.</summary>
internal sealed class WhatsAppSettingsReader(ApplicationDbContext context, ITenantContext tenant) : IWhatsAppSettingsReader
{
    public Task<WhatsAppPlatformSettingsRow?> FindPlatformAsync(CancellationToken ct) =>
        context.Set<WhatsAppPlatformSettings>().AsNoTracking().Where(value => value.Id == WhatsAppPlatformSettings.SingletonId)
            .Select(value => new WhatsAppPlatformSettingsRow(value.AllowedCountries, value.ChatRetentionDays,
                value.MaxOrganizationChatRetentionDays, value.Version))
            .SingleOrDefaultAsync(ct);

    public Task<WhatsAppOrganizationSettingsRow?> FindOrganizationAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequiredTenantId;
        return context.Set<WhatsAppOrganizationSettings>().AsNoTracking().Where(value => value.TenantId == tenantId)
            .Select(value => new WhatsAppOrganizationSettingsRow(value.ChatRetentionDays, value.Version))
            .SingleOrDefaultAsync(ct);
    }
}
