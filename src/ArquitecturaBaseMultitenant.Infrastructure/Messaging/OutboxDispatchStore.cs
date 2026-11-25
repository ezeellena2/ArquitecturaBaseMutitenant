using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// SKIP LOCKED protege el claim; el lease protege el envío entre transacciones. Lo que contesta sale antes que los avisos, y un
/// destino espera mientras tenga algo anterior pendiente o en curso (ADR 0066). Una reserva que venció queda «Sin confirmar» y
/// lo que venció su plazo se descarta; ninguno de los dos vuelve a salir.
/// </summary>
internal sealed class OutboxDispatchStore(ApplicationDbContext context, TimeProvider clock,
    IOptions<OutboxOptions> options, IAfterCommit afterCommit, OutboxMetrics metrics) : IOutboxDispatchStore
{
    public async Task<OutboxDispatchClaim?> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken ct)
    {
        context.RequireTransaction();
        if (channels.Count == 0) return null;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        var skipped = new List<Guid>();
        for (var scan = 0; scan < settings.BatchSize; scan++)
        {
            var ids = await LockIdsAsync(channels.ToArray(), skipped.ToArray(), nowUtc, ct);
            if (ids.Count == 0) return null;
            var message = await context.OutboxMessages.SingleAsync(value => value.Id == ids[0], ct);
            await context.Entry(message).ReloadAsync(ct);
            var claimed = message.TryClaim(Guid.CreateVersion7(), nowUtc, TimeSpan.FromSeconds(settings.LeaseSeconds),
                settings.MaxAttemptsFor(message.Channel));
            if (claimed == OutboxClaimResult.Claimed)
                return new OutboxDispatchClaim(message.Id, message.LeaseId!.Value, message.Channel, message.EncryptedPayload);
            if (claimed == OutboxClaimResult.Expired)
            {
                var channel = message.Channel;
                afterCommit.Add(() => metrics.Discarded(channel));
            }
            skipped.Add(message.Id);
        }
        return null;
    }

    public async Task<string?> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken ct)
    {
        var message = await LockAsync(messageId, ct);
        if (message is null || !message.TryComplete(leaseId, receipt.Outcome, receipt.ProviderMessageId, receipt.SafeCode,
                clock.GetUtcNow().UtcDateTime, TimeSpan.FromSeconds(options.Value.RetryBaseDelaySeconds),
                options.Value.MaxAttemptsFor(message.Channel))) return null;
        var channel = message.Channel;
        if (receipt.IsAccepted) afterCommit.Add(() => metrics.Sent(channel));
        return channel;
    }

    public async Task<bool> ReleaseAsync(Guid messageId, Guid leaseId, CancellationToken ct) =>
        await LockAsync(messageId, ct) is { } message && message.TryRelease(leaseId);

    private async Task<OutboxMessage?> LockAsync(Guid messageId, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = await context.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM platform."OutboxMessages" WHERE "Id" = {messageId} FOR UPDATE
            """).ToArrayAsync(ct);
        var message = rows.SingleOrDefault();
        if (message is not null) await context.Entry(message).ReloadAsync(ct);
        return message;
    }

    /// <summary>
    /// El próximo que le toca, por prioridad y por espera. Salta un destino con algo anterior (por creación) que sigue pendiente
    /// o en curso: lo que ya terminó no lo frena, y lo que venció su plazo tampoco, salvo que ya esté saliendo (tiene reserva:
    /// un envío en curso no se descarta). La reserva vencida se toma, para dejarla «Sin confirmar».
    /// </summary>
    private async Task<List<Guid>> LockIdsAsync(string[] channels, Guid[] skipped, DateTime nowUtc, CancellationToken ct)
    {
        var transaction = context.Database.CurrentTransaction!;
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SELECT m."Id" FROM platform."OutboxMessages" m
            WHERE m."Status" IN ('Pending', 'Failed') AND m."NextAttemptAtUtc" <= @nowUtc
              AND (m."LeaseExpiresAtUtc" IS NULL OR m."LeaseExpiresAtUtc" <= @nowUtc)
              AND m."Channel" = ANY(@channels)
              AND NOT (m."Id" = ANY(@skipped))
              AND (m."OrderingKey" IS NULL OR NOT EXISTS (
                SELECT 1 FROM platform."OutboxMessages" e
                WHERE e."OrderingKey" = m."OrderingKey" AND e."Status" IN ('Pending', 'Failed')
                  AND e."NextAttemptAtUtc" IS NOT NULL
                  AND (e."DiscardAfterUtc" IS NULL OR e."DiscardAfterUtc" > @nowUtc OR e."LeaseId" IS NOT NULL)
                  AND (e."CreatedAtUtc", e."Id") < (m."CreatedAtUtc", m."Id")))
            ORDER BY m."Priority", m."NextAttemptAtUtc", m."Id" LIMIT 1 FOR UPDATE OF m SKIP LOCKED
            """;
        AddParameter(command, "nowUtc", nowUtc); AddParameter(command, "channels", channels);
        AddParameter(command, "skipped", skipped);
        var ids = new List<Guid>(1);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0));
        return ids;
    }
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
