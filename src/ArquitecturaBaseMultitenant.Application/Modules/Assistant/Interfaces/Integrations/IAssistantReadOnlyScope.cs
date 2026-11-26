namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;

/// <summary>
/// El alcance de solo lectura del asistente (ADR 0068 §5): un alcance de DI propio donde corren las herramientas de lectura
/// y las fuentes de las acciones. Entra con el actor revalidado del worker o con la organización y la persona de la petición,
/// nunca con <c>ITenantScope.Enter</c>; abre una transacción de PostgreSQL <c>READ ONLY</c> y se descarta al terminar, así
/// nada que quede en memoria llega a otra transacción. Una escritura adentro falla: la base la rechaza y el
/// <c>IUnitOfWork</c> del alcance no abre otra transacción.
/// </summary>
public interface IAssistantReadOnlyScope
{
    /// <summary>Resuelve <typeparamref name="TService"/> en un alcance de solo lectura nuevo y corre <paramref name="work"/> con él.</summary>
    Task<TResult> RunAsync<TService, TResult>(Func<TService, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken)
        where TService : notnull;
}
