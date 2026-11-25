using System.Data.Common;
using System.Diagnostics;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Dos actores a la vez contra un lock (puerta, punto 8). Retiene antes de su commit la primera transacción cuyo contexto cumple
/// la condición, con lo suyo ya escrito, hasta que otra sesión de la base queda esperando algo que esa transacción tiene (un
/// lock advisory, una fila o una clave única) o el segundo actor termina sin esperar.
/// Con el lock, el segundo espera y lee lo confirmado; sin él, lee lo de antes y choca con lo del primero o lo pisa, y el test lo
/// ve en las respuestas.
/// </summary>
internal sealed class CommitHold(Func<DbContext, bool> holds) : DbTransactionInterceptor
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _state;
    private int _firstProcessId;

    /// <summary>Suma este interceptor al contexto de la app del host.</summary>
    public void Install(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
        services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(this)));

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && Volatile.Read(ref _state) == 1 && holds(context)
            && Interlocked.CompareExchange(ref _state, 2, 1) == 1)
        {
            Volatile.Write(ref _firstProcessId, ((NpgsqlConnection)transaction.Connection!).ProcessID);
            _held.TrySetResult();
            await _released.Task.WaitAsync(Patience, cancellationToken);
        }
        return result;
    }

    /// <summary>
    /// Corre el primero hasta su commit, después el segundo hasta que espera un lock o termina, suelta al primero y devuelve las
    /// dos tareas terminadas, con su resultado o su excepción. <paramref name="services"/> es el del host, para mirar la base.
    /// </summary>
    internal async Task<(Task<T> First, Task<T> Second)> RaceAsync<T>(IServiceProvider services, Func<Task<T>> first,
        Func<Task<T>> second, CancellationToken ct)
    {
        Interlocked.Exchange(ref _state, 1);
        var one = first();
        await Task.WhenAny(_held.Task, one).WaitAsync(Patience, ct);
        Assert.True(_held.Task.IsCompleted, "The first actor never reached its commit.");
        var two = second();
        var waited = Stopwatch.StartNew();
        while (!two.IsCompleted && waited.Elapsed < Patience && !await BlockedByFirstAsync(services, Volatile.Read(ref _firstProcessId), ct))
            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        _released.TrySetResult();
        await Task.WhenAll(one, two).ContinueWith(_ => { }, ct, TaskContinuationOptions.None, TaskScheduler.Default).WaitAsync(Patience, ct);
        return (one, two);
    }

    /// <summary>Si alguna sesión espera un lock que tiene la transacción retenida del primero.</summary>
    private static async Task<bool> BlockedByFirstAsync(IServiceProvider services, int first, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.SqlQuery<int>(
            $"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE {first} = ANY(pg_blocking_pids(pid))""")
            .SingleAsync(ct) > 0;
    }
}
