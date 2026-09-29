using Npgsql;
using NpgsqlTypes;

namespace ArquitecturaBaseMultitenant.Infrastructure.Idempotency;

public enum IdempotencyReservationState
{
    Reserved,
    InProgress,
    Reused,
    Replay,
}

public sealed record IdempotencyReservation(
    Guid Id,
    IdempotencyReservationState State,
    int? ResponseStatusCode = null,
    string? ResponseBody = null);

public sealed class IdempotencyStore
{
    private static readonly TimeSpan Validity = TimeSpan.FromHours(24);
    private readonly string _connectionString;
    private readonly TimeProvider _timeProvider;

    public IdempotencyStore(string connectionString, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _connectionString = connectionString;
        _timeProvider = timeProvider;
    }

    /// <summary>Inserta la reserva antes de ejecutar la acción, en una transacción ajena al UoW.</summary>
    public async Task<IdempotencyReservation> ReserveAsync(
        Guid? tenantId,
        Guid userId,
        Guid key,
        string bodyHash,
        string route,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        if (key == Guid.Empty)
        {
            throw new ArgumentException("An idempotency key cannot be empty.", nameof(key));
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var expired = connection.CreateCommand())
        {
            expired.Transaction = transaction;
            expired.CommandText = """
                DELETE FROM platform."IdempotencyKeys"
                WHERE "TenantId" IS NOT DISTINCT FROM @tenant
                  AND "UserId" = @user AND "Key" = @key AND "ExpiresAtUtc" <= @now
                """;
            AddScope(expired, tenantId, userId, key);
            expired.Parameters.AddWithValue("now", nowUtc);
            await expired.ExecuteNonQueryAsync(cancellationToken);
        }

        var id = Guid.NewGuid();
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO platform."IdempotencyKeys"
                  ("Id", "TenantId", "UserId", "Key", "BodyHash", "Route", "CreatedAtUtc", "ExpiresAtUtc")
                VALUES (@id, @tenant, @user, @key, @bodyHash, @route, @now, @expires)
                ON CONFLICT ("TenantId", "UserId", "Key") DO NOTHING
                RETURNING "Id"
                """;
            insert.Parameters.AddWithValue("id", id);
            AddScope(insert, tenantId, userId, key);
            insert.Parameters.AddWithValue("bodyHash", bodyHash);
            insert.Parameters.AddWithValue("route", route);
            insert.Parameters.AddWithValue("now", nowUtc);
            insert.Parameters.AddWithValue("expires", nowUtc.Add(Validity));
            if (await insert.ExecuteScalarAsync(cancellationToken) is Guid insertedId)
            {
                await transaction.CommitAsync(cancellationToken);
                return new IdempotencyReservation(insertedId, IdempotencyReservationState.Reserved);
            }
        }

        Guid existingId;
        bool sameRequest;
        int? responseStatusCode;
        string? responseBody;
        await using (var existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = """
                SELECT "Id", "BodyHash", "Route", "ResponseStatusCode", "ResponseBody"
                FROM platform."IdempotencyKeys"
                WHERE "TenantId" IS NOT DISTINCT FROM @tenant AND "UserId" = @user AND "Key" = @key
                """;
            AddScope(existing, tenantId, userId, key);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("An idempotency reservation disappeared during conflict resolution.");
            }

            existingId = reader.GetGuid(0);
            sameRequest = string.Equals(reader.GetString(1), bodyHash, StringComparison.Ordinal)
                && string.Equals(reader.GetString(2), route, StringComparison.Ordinal);
            responseStatusCode = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            responseBody = reader.IsDBNull(4) ? null : reader.GetString(4);
        }

        await transaction.CommitAsync(cancellationToken);

        return sameRequest
            ? responseStatusCode is { } completedStatus
                ? new IdempotencyReservation(existingId, IdempotencyReservationState.Replay, completedStatus, responseBody)
                : new IdempotencyReservation(existingId, IdempotencyReservationState.InProgress)
            : new IdempotencyReservation(existingId, IdempotencyReservationState.Reused);
    }

    /// <summary>Se llama solo después de que terminó el UoW de la acción.</summary>
    public async Task CompleteAsync(Guid id, int statusCode, string responseBody, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE platform."IdempotencyKeys"
            SET "ResponseStatusCode" = @status, "ResponseBody" = @body
            WHERE "Id" = @id AND "ResponseStatusCode" IS NULL
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("status", statusCode);
        command.Parameters.AddWithValue("body", responseBody);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The idempotency reservation could not be completed.");
        }
    }

    public async Task ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM platform.\"IdempotencyKeys\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM platform.\"IdempotencyKeys\" WHERE \"ExpiresAtUtc\" <= @now";
        command.Parameters.AddWithValue("now", _timeProvider.GetUtcNow().UtcDateTime);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddScope(NpgsqlCommand command, Guid? tenantId, Guid userId, Guid key)
    {
        command.Parameters.AddWithValue("tenant", NpgsqlDbType.Uuid, (object?)tenantId ?? DBNull.Value);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("key", key);
    }
}
