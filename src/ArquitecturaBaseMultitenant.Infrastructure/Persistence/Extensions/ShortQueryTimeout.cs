using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>
/// Un timeout corto para las consultas de una lista que arma otra área (los miembros y los contactos de un catálogo, E12): un
/// filtro que tarda corta a los 3 s con un error, en lugar de ocupar la conexión. Se aplica al contexto del pedido mientras dura
/// el bloque, y al salir vuelve el que tenía.
/// </summary>
public static class ShortQueryTimeout
{
    public static readonly TimeSpan Value = TimeSpan.FromSeconds(3);

    public static IDisposable Apply(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = context.Database.GetCommandTimeout();
        context.Database.SetCommandTimeout(Value);
        return new Restore(context, previous);
    }

    private sealed class Restore(DbContext context, int? previous) : IDisposable
    {
        public void Dispose() => context.Database.SetCommandTimeout(previous);
    }
}
