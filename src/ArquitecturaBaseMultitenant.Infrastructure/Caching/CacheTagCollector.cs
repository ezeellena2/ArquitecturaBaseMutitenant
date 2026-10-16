using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Junta, en cada guardado, las etiquetas de caché que avanzan sus cambios rastreados (ADR 0052). Va último entre los
/// interceptores: ve las filas ya selladas con su organización. Ningún escritor invalida a mano; un seed, un worker o
/// Identity pasan por el mismo guardado. Un guardado que falla no descarta nada: si la transacción sigue y confirma,
/// avanzar de más sólo cuesta un miss.
/// </summary>
internal sealed class CacheTagCollector(PendingCacheTags pending) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Collect(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
            pending.Add(context.ChangeTracker.Entries().SelectMany(CacheTags.Changed));
    }
}
