using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Avanza, antes del commit y en la misma transacción, la generación de cada etiqueta juntada al guardar; después del
/// commit las invalida en Redis (ADR 0052). Un rollback descarta las dos cosas: la generación vuelve con la transacción y
/// nada se invalida. También vale para la transacción implícita de un guardado sin unidad de trabajo. El invalidador se
/// resuelve recién al confirmar: construir un DbContext no levanta Redis.
/// </summary>
internal sealed class CacheGenerationInterceptor(PendingCacheTags pending, Lazy<CacheTagInvalidator> invalidator)
    : DbTransactionInterceptor
{
    // Las etiquetas llegan ordenadas: dos transacciones toman los locks de sus filas en el mismo orden.
    private const string AdvanceSql = """
        INSERT INTO platform."CacheTagVersions" ("Tag", "Generation")
        SELECT tag, 1 FROM unnest(@tags) WITH ORDINALITY AS advanced(tag, position) ORDER BY position
        ON CONFLICT ("Tag") DO UPDATE SET "Generation" = platform."CacheTagVersions"."Generation" + 1
        """;

    private string[] _advanced = [];

    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result)
    {
        if (TakePending() is { Length: > 0 } tags)
        {
            using var command = Advance(transaction, tags);
            command.ExecuteNonQuery();
        }

        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (TakePending() is { Length: > 0 } tags)
        {
            await using var command = Advance(transaction, tags);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return result;
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        Invalidate(TakeAdvanced()).GetAwaiter().GetResult();

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) => Invalidate(TakeAdvanced());

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Discard();

    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Discard();
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Discard();

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Discard();
        return Task.CompletedTask;
    }

    private string[] TakePending() => _advanced = pending.Take();

    private Task Invalidate(string[] tags) => tags.Length == 0 ? Task.CompletedTask : invalidator.Value.InvalidateAsync(tags);

    private string[] TakeAdvanced()
    {
        var advanced = _advanced;
        _advanced = [];
        return advanced;
    }

    private void Discard()
    {
        pending.Clear();
        _advanced = [];
    }

    private static DbCommand Advance(DbTransaction transaction, string[] tags)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = AdvanceSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tags";
        parameter.Value = tags;
        command.Parameters.Add(parameter);
        return command;
    }
}
