using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Forms.Persistence.Repositories;

/// <summary>
/// La barrera de C11 sobre <c>tenant."FormWorkLeases"</c>, en una conexión y una transacción propias, como la reserva de
/// <c>IdempotencyStore</c>: el <c>INSERT … ON CONFLICT</c> pisa solo la barrera vencida de la persona, y después cuenta las
/// vigentes de la organización y de la instalación; sin lugar, deshace la toma. Las tomas de todas las copias se ordenan con un
/// advisory lock que dura lo que esa transacción corta, nunca lo que dura la operación de archivo: así dos que llegan juntas no
/// cuentan las dos el mismo lugar. La de la instalación la cuenta <c>tenant.count_form_work_leases</c>, que ve todas las
/// organizaciones y devuelve solo un número. Mientras la operación sigue (una subida lenta puede durar más que el vencimiento), la
/// copia renueva su barrera cada <see cref="RenewEvery"/>, como <c>RedisExclusiveSlots</c>; si la copia se cae, vence sola.
/// </summary>
internal sealed partial class FormWorkLeaseStore(
    string connectionString,
    ITenantContext tenant,
    TimeProvider timeProvider,
    IOptions<FormsOptions> options,
    ILogger<FormWorkLeaseStore> logger) : IFormWorkLease
{
    // Un lugar de la organización o de la instalación se libera cuando termina otra operación de archivo.
    internal const int RetryAfterSeconds = 5;
    internal const string TakersLock = "forms:catalog-files";
    internal static readonly TimeSpan RenewEvery = FormWorkLease.Lifetime / 4;

    public async Task<Result<IAsyncDisposable>> TryTakeAsync(Guid userId, FormWorkLeaseKind kind, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var tenantId = tenant.RequiredTenantId;
        var limits = options.Value.CatalogFiles;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var lease = FormWorkLease.Create(userId, kind, nowUtc);

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await ExecuteAsync(connection, transaction, """
                SELECT set_config('app.tenant_id', @tenant, true), pg_advisory_xact_lock(hashtextextended(@lock, 0))
                """, ct, ("tenant", tenantId.ToString("D")), ("lock", TakersLock));

            var taken = await ScalarAsync(connection, transaction, """
                INSERT INTO tenant."FormWorkLeases" ("Id", "TenantId", "UserId", "Kind", "ExpiresAtUtc")
                VALUES (@id, @tenant, @user, @kind, @expires)
                ON CONFLICT ("TenantId", "UserId") DO UPDATE
                SET "Id" = EXCLUDED."Id", "Kind" = EXCLUDED."Kind", "ExpiresAtUtc" = EXCLUDED."ExpiresAtUtc"
                WHERE tenant."FormWorkLeases"."ExpiresAtUtc" <= @now
                RETURNING "Id"
                """, ct, ("id", lease.Id), ("tenant", tenantId), ("user", userId), ("kind", kind.ToString()),
                ("expires", lease.ExpiresAtUtc), ("now", nowUtc));
            if (taken is not Guid)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<IAsyncDisposable>(FormCatalogImportErrors.Busy);
            }

            // Las cuentas incluyen la barrera recién tomada.
            var organization = (long)(await ScalarAsync(connection, transaction, """
                SELECT count(*) FROM tenant."FormWorkLeases" WHERE "TenantId" = @tenant AND "ExpiresAtUtc" > @now
                """, ct, ("tenant", tenantId), ("now", nowUtc)))!;
            var installation = (int)(await ScalarAsync(connection, transaction,
                "SELECT tenant.count_form_work_leases(@now)", ct, ("now", nowUtc)))!;
            if (organization > limits.MaxPerOrganization || installation > limits.MaxPerInstallation)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure<IAsyncDisposable>(FormCatalogImportErrors.TooManyAtOnce(RetryAfterSeconds));
            }

            await transaction.CommitAsync(ct);
        }

        return Result.Success<IAsyncDisposable>(new Lease(connectionString, tenantId, lease.Id, timeProvider, logger));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, sql, parameters);
        return await command.ExecuteScalarAsync(ct);
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    /// <summary>
    /// La barrera tomada: la renueva mientras dura la operación y la suelta por su id, una sola vez y aunque el pedido se haya
    /// cortado. Si venció y la persona ya tomó otra, esa tiene otro id: ni se renueva ni se suelta.
    /// </summary>
    private sealed class Lease : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly Guid _tenantId;
        private readonly Guid _leaseId;
        private readonly TimeProvider _clock;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _renewal;
        private int _released;

        public Lease(string connectionString, Guid tenantId, Guid leaseId, TimeProvider clock, ILogger logger)
        {
            (_connectionString, _tenantId, _leaseId, _clock, _logger) = (connectionString, tenantId, leaseId, clock, logger);
            // Sin el contexto del pedido; la primera espera queda anotada antes de devolver la barrera.
            using (ExecutionContext.SuppressFlow()) _renewal = RenewAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            await _stop.CancelAsync();
            await _renewal;
            _stop.Dispose();
            await using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync(CancellationToken.None);
                await using var transaction = await connection.BeginTransactionAsync(CancellationToken.None);
                await ExecuteAsync(connection, transaction, "SELECT set_config('app.tenant_id', @tenant, true)",
                    CancellationToken.None, ("tenant", _tenantId.ToString("D")));
                await ExecuteAsync(connection, transaction, """
                    DELETE FROM tenant."FormWorkLeases" WHERE "TenantId" = @tenant AND "Id" = @id
                    """, CancellationToken.None, ("tenant", _tenantId), ("id", _leaseId));
                await transaction.CommitAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Corre el vencimiento cada <see cref="RenewEvery"/> hasta que se suelta. La espera siguiente se anota antes de renovar;
        /// una renovación que falla se avisa y se reintenta en la siguiente; si la barrera ya no es suya, deja de renovar.
        /// </summary>
        private async Task RenewAsync()
        {
            try
            {
                var next = Task.Delay(RenewEvery, _clock, _stop.Token);
                while (true)
                {
                    await next;
                    next = Task.Delay(RenewEvery, _clock, _stop.Token);
                    try
                    {
                        if (!await RenewOnceAsync()) return;
                    }
                    catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
                    {
                        LogRenewalFailed(_logger, exception.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                // Soltada: deja de renovar.
            }
        }

        private async Task<bool> RenewOnceAsync()
        {
            var expiresAtUtc = _clock.GetUtcNow().UtcDateTime + FormWorkLease.Lifetime;
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(_stop.Token);
            await using var transaction = await connection.BeginTransactionAsync(_stop.Token);
            await ExecuteAsync(connection, transaction, "SELECT set_config('app.tenant_id', @tenant, true)", _stop.Token,
                ("tenant", _tenantId.ToString("D")));
            await using var command = Command(connection, transaction, """
                UPDATE tenant."FormWorkLeases" SET "ExpiresAtUtc" = @expires WHERE "TenantId" = @tenant AND "Id" = @id
                """, [("expires", expiresAtUtc), ("tenant", _tenantId), ("id", _leaseId)]);
            var renewed = await command.ExecuteNonQueryAsync(_stop.Token) == 1;
            await transaction.CommitAsync(_stop.Token);
            return renewed;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog file barrier renewal failed: {ExceptionType}")]
    private static partial void LogRenewalFailed(ILogger logger, string exceptionType);
}
