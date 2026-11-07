using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Cuenta las consultas de un contexto, para probar que una lectura no crece con sus filas (sin N+1): hace las mismas
/// consultas con una fila que con muchas. Se suma al host con <c>AddDbContext(options =&gt; options.AddInterceptors(...))</c>
/// y cuenta solo lo que lee <see cref="Target"/>, el contexto del alcance que se mide.
/// </summary>
internal sealed class QueryCounter : DbCommandInterceptor
{
    private int count;

    public DbContext? Target { get; set; }

    public int Count => Volatile.Read(ref count);

    public void Reset() => Interlocked.Exchange(ref count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Track(eventData);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Track(eventData);
        return ValueTask.FromResult(result);
    }

    private void Track(CommandEventData eventData)
    {
        if (Target is not null && ReferenceEquals(eventData.Context, Target)) Interlocked.Increment(ref count);
    }
}
