namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Organization;

/// <summary>
/// Observa cambios de acceso o permisos de organización de los miembros dentro de la UoW del núcleo (B12): se invoca cuando a
/// alguien lo suspenden, lo quitan, le cambian los roles o se edita un rol que tiene. Recibe una sola vez por caso a todas las
/// personas afectadas, cada una con los permisos efectivos que le quedan en la organización (vacío si dejó de estar activa),
/// así lee y bloquea lo de todas de una vez.
/// </summary>
public interface IMemberAccessObserver
{
    Task AccessChangedAsync(IReadOnlyDictionary<Guid, IReadOnlyCollection<string>> effectivePermissionsByUser, CancellationToken ct);
}
