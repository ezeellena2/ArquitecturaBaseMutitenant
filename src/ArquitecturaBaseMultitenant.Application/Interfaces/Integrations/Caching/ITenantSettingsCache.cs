namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Invalida las preferencias de la organización después de guardar cambios confirmados.</summary>
public interface ITenantSettingsCache
{
    ValueTask InvalidateAsync(Guid tenantId, CancellationToken cancellationToken);
}
