using System.Data.Common;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Locks one due row with SKIP LOCKED and tracks its send within the caller's transaction.</summary>
internal sealed partial class OutboxDispatchStore(
    ApplicationDbContext context,
    IPayloadProtector protector,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatchStore> logger) : IOutboxDispatchStore
{
    public async Task<int> DispatchDueAsync(IReadOnlyCollection<IChannelSender> senders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(senders);
        if (senders.Count == 0)
        {
            return 0;
        }

        var byChannel = senders.ToDictionary(sender => sender.Key, StringComparer.Ordinal);
        var settings = options.Value;
        var ids = await LockDueIdsAsync(byChannel.Keys.ToArray(), timeProvider.GetUtcNow().UtcDateTime,
            1, cancellationToken);
        if (ids.Count == 0)
        {
            return 0;
        }

        var messages = await context.OutboxMessages
            .Where(message => ids.Contains(message.Id))
            .ToDictionaryAsync(message => message.Id, cancellationToken);
        foreach (var id in ids)
        {
            var message = messages[id];
            try
            {
                var payload = protector.Unprotect(message.EncryptedPayload);
                await byChannel[message.Channel].SendAsync(payload, cancellationToken);
                message.MarkSent(timeProvider.GetUtcNow().UtcDateTime);
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                message.RecordFailure(timeProvider.GetUtcNow().UtcDateTime,
                    TimeSpan.FromSeconds(settings.RetryBaseDelaySeconds), settings.MaxAttempts);
                LogDeliveryFailed(logger, message.Id, exception.GetType().Name);
            }
        }

        return ids.Count;
    }

    private async Task<List<Guid>> LockDueIdsAsync(string[] channels, DateTime nowUtc,
        int batchSize, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("The dispatcher needs a UnitOfWork transaction.");
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            SELECT "Id" FROM platform."OutboxMessages"
            WHERE "Status" IN ('Pending', 'Failed')
              AND "NextAttemptAtUtc" <= @nowUtc
              AND "Channel" = ANY(@channels)
            ORDER BY "NextAttemptAtUtc", "Id"
            LIMIT @batchSize FOR UPDATE SKIP LOCKED
            """;
        AddParameter(command, "nowUtc", nowUtc);
        AddParameter(command, "channels", channels);
        AddParameter(command, "batchSize", batchSize);

        var ids = new List<Guid>(batchSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} failed with {ExceptionType}")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, string exceptionType);
}
