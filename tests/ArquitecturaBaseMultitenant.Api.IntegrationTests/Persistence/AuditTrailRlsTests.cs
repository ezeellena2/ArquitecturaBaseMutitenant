using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// Comprueba las políticas RLS del rastro de auditoría y su rollback con el caso de uso. Protege las
/// inserciones autorizadas sin abrir la lectura a la contraparte.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AuditTrailRlsTests(ApiFactory factory)
{
    [Fact]
    public async Task Explicit_audit_event_rolls_back_with_the_application_transaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, cancellationToken);

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection)
                .Options;
            await using var context = new ApplicationDbContext(options, new TestTenantContext(tenantId));
            await context.Database.UseTransactionAsync(transaction, cancellationToken);
            var log = new AuditLog(context, new TestTenantContext(tenantId), new TestCurrentUser(),
                TimeProvider.System);
            log.Record(AuditAction.Custom, "Export", subjectId,
                new Dictionary<string, object?> { ["result"] = "Completed" });

            await context.SaveChangesAsync(cancellationToken);
            Assert.Equal(1L, await CountAsync(connection, transaction, subjectId, cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
        }

        Assert.Equal(0L, await CountAsync(connection, null, subjectId, cancellationToken));
    }

    [Fact]
    public async Task Counterpart_insert_policy_allows_only_the_transaction_local_batch_and_keeps_reads_private()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var activeTenant = Guid.NewGuid();
        var firstCounterpart = Guid.NewGuid();
        var secondCounterpart = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, activeTenant, cancellationToken);

        await using (var blocked = await connection.BeginTransactionAsync(cancellationToken))
        {
            Func<Task> insert = () => InsertAsync(connection, blocked, firstCounterpart, entityId, cancellationToken);
            var exception = await Assert.ThrowsAsync<PostgresException>(insert);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
            await blocked.RollbackAsync(cancellationToken);
        }

        await using (var permitted = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var setting = new NpgsqlCommand(
                "SELECT set_config('app.audit_counterpart_tenant_ids', @tenant_ids, true)", connection, permitted))
            {
                setting.Parameters.AddWithValue("tenant_ids", $"{firstCounterpart:D},{secondCounterpart:D}");
                await setting.ExecuteScalarAsync(cancellationToken);
            }

            await InsertAsync(connection, permitted, activeTenant, entityId, cancellationToken);
            await InsertAsync(connection, permitted, firstCounterpart, entityId, cancellationToken);
            await InsertAsync(connection, permitted, secondCounterpart, entityId, cancellationToken);
            Assert.Equal(1L, await CountAsync(connection, permitted, entityId, cancellationToken));
            await permitted.CommitAsync(cancellationToken);
        }

        await using (var check = new NpgsqlCommand(
            "SELECT current_setting('app.audit_counterpart_tenant_ids', true)", connection))
        {
            var leftover = await check.ExecuteScalarAsync(cancellationToken);
            Assert.True(leftover is null or DBNull or string { Length: 0 });
        }

        await using (var blockedAgain = await connection.BeginTransactionAsync(cancellationToken))
        {
            Func<Task> insert = () => InsertAsync(connection, blockedAgain, firstCounterpart, Guid.NewGuid(), cancellationToken);
            var exception = await Assert.ThrowsAsync<PostgresException>(insert);
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
            await blockedAgain.RollbackAsync(cancellationToken);
        }

        await using var counterpartConnection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, firstCounterpart, cancellationToken);
        Assert.Equal(1L, await CountAsync(counterpartConnection, null, entityId, cancellationToken));
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."AuditEntries"
                ("Id", "TenantId", "ActorKind", "ActorId", "Action", "EntityType", "EntityId", "Changes", "OccurredAtUtc")
            VALUES (@id, @tenant_id, 'System', NULL, 'Custom', 'AuditPolicyTest', @entity_id, '{}'::jsonb, @occurred_at)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("occurred_at", new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenant.\"AuditEntries\" WHERE \"EntityId\" = @entity_id", connection, transaction);
        command.Parameters.AddWithValue("entity_id", entityId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
