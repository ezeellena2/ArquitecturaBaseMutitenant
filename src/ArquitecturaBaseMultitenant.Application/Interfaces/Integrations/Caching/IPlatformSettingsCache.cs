namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Invalida las reglas globales del registro después de un cambio confirmado.</summary>
public interface IPlatformSettingsCache
{
    ValueTask InvalidateAsync(CancellationToken cancellationToken);
}
