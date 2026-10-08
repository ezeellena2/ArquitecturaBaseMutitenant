namespace ArquitecturaBaseMultitenant.Api.Features;

/// <summary>Cada composición de módulo declara sus rutas técnicas y su disponibilidad.</summary>
public interface IInstalledModuleRouteAvailability
{
    string Key { get; }
    bool IsAvailable { get; }
}
