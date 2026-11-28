namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization;

/// <summary>
/// Observa cambios de acceso o permisos de organización de un miembro dentro de la UoW del núcleo (B12): se invoca
/// cuando lo suspenden, lo quitan, le cambian los roles o se edita un rol que tiene, con los permisos efectivos que le
/// quedan en la organización (vacío si dejó de estar activo).
/// </summary>
public interface IMemberAccessObserver
{
    Task AccessChangedAsync(Guid userId, IReadOnlyCollection<string> effectivePermissions, CancellationToken ct);
}
