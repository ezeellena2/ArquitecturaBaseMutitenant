namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// La base que está leyendo o guardando en este momento del flujo. La fijan la consulta (<see cref="OperationQueryCompiler"/>)
/// y el guardado de <see cref="ApplicationDbContext"/> mientras corren, así un conversor que depende del alcance, como el del
/// contenido cifrado de un chat (ADR 0065), toma la organización de esa base: la misma que fija <c>app.tenant_id</c>, nunca la de
/// otra base que corre al lado en el mismo flujo ni la de la fila. EF arma el modelo una vez por proceso, así que un conversor no
/// puede recibir servicios: los busca acá en cada conversión. Fuera de una consulta o un guardado no hay base, y quien la pide
/// falla.
/// </summary>
internal static class PersistenceOperation
{
    private static readonly AsyncLocal<ApplicationDbContext?> Active = new();

    internal static ApplicationDbContext? Current => Active.Value;

    /// <summary>Fija la base hasta que se suelta; al soltarla vuelve la que había, así una consulta dentro de otra no la pisa.</summary>
    internal static Binding Enter(ApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = Active.Value;
        Active.Value = context;
        return new Binding(previous);
    }

    internal readonly struct Binding(ApplicationDbContext? previous) : IDisposable
    {
        public void Dispose() => Active.Value = previous;
    }
}
