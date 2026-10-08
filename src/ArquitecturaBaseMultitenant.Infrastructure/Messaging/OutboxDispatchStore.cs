using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>SKIP LOCKED protege el claim; el lease protege el envío entre transacciones.</summary>
internal sealed class OutboxDispatchStore(ApplicationDbContext context, TimeProvider clock,
    IOptions<OutboxOptions> options) : IOutboxDispatchStore
{
    public async Task<OutboxDispatchClaim?> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken ct)
    {
        context.RequireTransaction();
        if (channels.Count == 0) return null;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var exhausted = new List<Guid>();
        for (var scan = 0; scan < options.Value.BatchSize; scan++)
        {
            var ids = await LockIdsAsync(channels.ToArray(), exhausted.ToArray(), nowUtc, ct);
            if (ids.Count == 0) return null;
            var message = await context.OutboxMessages.SingleAsync(value => value.Id == ids[0], ct);
            await context.Entry(message).ReloadAsync(ct);
            if (!message.TryClaim(Guid.CreateVersion7(), nowUtc, TimeSpan.FromSeconds(options.Value.LeaseSeconds), options.Value.MaxAttempts))
            { exhausted.Add(message.Id); continue; }
            return new OutboxDispatchClaim(message.Id, message.LeaseId!.Value, message.Channel, message.EncryptedPayload);
        }
        return null;
    }

    public async Task<string?> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken ct)
    {
        context.RequireTransaction();
        var rows = await context.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM platform."OutboxMessages" WHERE "Id" = {messageId} FOR UPDATE
            """).ToArrayAsync(ct);
        var message = rows.SingleOrDefault();
        if (message is not null) await context.Entry(message).ReloadAsync(ct);
        if (message is null || !message.TryComplete(leaseId, receipt.IsAccepted, receipt.IsRetryable,
                receipt.ProviderMessageId, receipt.SafeCode, clock.GetUtcNow().UtcDateTime,
                TimeSpan.FromSeconds(options.Value.RetryBaseDelaySeconds), options.Value.MaxAttempts)) return null;
        return message.Channel;
    }

    private async Task<List<Guid>> LockIdsAsync(string[] channels, Guid[] exhausted, DateTime nowUtc, CancellationToken ct)
    {
        var transaction = context.Database.CurrentTransaction!;
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SELECT "Id" FROM platform."OutboxMessages"
            WHERE "Status" IN ('Pending', 'Failed') AND "NextAttemptAtUtc" <= @nowUtc
              AND ("LeaseExpiresAtUtc" IS NULL OR "LeaseExpiresAtUtc" <= @nowUtc)
              AND "Channel" = ANY(@channels)
              AND NOT ("Id" = ANY(@exhausted))
            ORDER BY "NextAttemptAtUtc", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """;
        AddParameter(command, "nowUtc", nowUtc); AddParameter(command, "channels", channels);
        AddParameter(command, "exhausted", exhausted);
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
