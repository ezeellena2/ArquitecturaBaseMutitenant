using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que la proyección global siga los cambios de membresía y su rollback. Exige permisos de solo
/// lectura runtime y backfill de datos existentes.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class UserTenantAccessIndexTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Index_follows_member_create_status_change_delete_and_rollback()
    {
        using var client = factory.CreateClient();
        Guid userId;
        Tenant tenant;
        await using (var globalScope = factory.Services.CreateAsyncScope())
        {
            var services = globalScope.ServiceProvider;
            userId = Guid.Empty;
            tenant = Tenant.CreateBusiness("Index test", requiresApproval: false);
            Assert.True(tenant.Activate().IsSuccess);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                userId = (await services.GetRequiredService<IUserRepository>()
                    .CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                services.GetRequiredService<ITenantRepository>().Add(tenant);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var joinedAtUtc = new DateTime(2026, 9, 29, 15, 0, 0, DateTimeKind.Utc);
        await using (var memberScope = factory.Services.CreateAsyncScope())
        {
            var services = memberScope.ServiceProvider;
            using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var member = Member.Invite(userId);
                Assert.True(member.Activate(joinedAtUtc).IsSuccess);
                services.GetRequiredService<IMemberRepository>().Add(member);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        Assert.Equal((MemberStatus.Active.ToString(), joinedAtUtc),
            await ReadIndexAsync(userId, tenant.Id));

        await using (var memberScope = factory.Services.CreateAsyncScope())
        {
            var services = memberScope.ServiceProvider;
            using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var member = await services.GetRequiredService<IMemberRepository>()
                    .GetByUserIdAsync(userId, ct);
                Assert.NotNull(member);
                Assert.True(member.Deactivate().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        Assert.Equal((MemberStatus.Inactive.ToString(), joinedAtUtc),
            await ReadIndexAsync(userId, tenant.Id));

        await using (var runtime = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, tenant.Id, Ct))
        await using (var transaction = await runtime.BeginTransactionAsync(Ct))
        {
            await using var update = new NpgsqlCommand("""
                UPDATE tenant."Members" SET "Status" = 'Active'
                WHERE "TenantId" = @tenant_id AND "UserId" = @user_id
                """, runtime, transaction);
            update.Parameters.AddWithValue("tenant_id", tenant.Id);
            update.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await update.ExecuteNonQueryAsync(Ct));
            Assert.Equal("Active", await ReadIndexStatusAsync(runtime, transaction, userId, tenant.Id));
            await transaction.RollbackAsync(Ct);
        }

        Assert.Equal((MemberStatus.Inactive.ToString(), joinedAtUtc),
            await ReadIndexAsync(userId, tenant.Id));

        await using (var admin = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant_id, false)", admin))
            {
                scope.Parameters.AddWithValue("tenant_id", tenant.Id.ToString("D"));
                await scope.ExecuteNonQueryAsync(Ct);
            }
            await using var delete = new NpgsqlCommand("""
                DELETE FROM tenant."Members" WHERE "TenantId" = @tenant_id AND "UserId" = @user_id
                """, admin);
            delete.Parameters.AddWithValue("tenant_id", tenant.Id);
            delete.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await delete.ExecuteNonQueryAsync(Ct));
        }

        Assert.Null(await ReadIndexAsync(userId, tenant.Id));
    }

    [Fact]
    public async Task Runtime_role_can_read_index_but_cannot_write_it_directly()
    {
        using var client = factory.CreateClient();
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT has_table_privilege(current_user, 'identity."UserTenantAccesses"', 'SELECT'),
                   has_table_privilege(current_user, 'identity."UserTenantAccesses"', 'INSERT'),
                   has_table_privilege(current_user, 'identity."UserTenantAccesses"', 'UPDATE'),
                   has_table_privilege(current_user, 'identity."UserTenantAccesses"', 'DELETE')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    [Fact]
    public async Task Migration_backfills_memberships_from_two_existing_tenants()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient();
        await using var migrationScope = isolated.Services.CreateAsyncScope();
        var tenantContext = migrationScope.ServiceProvider.GetRequiredService<ITenantContext>();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(isolated.AdminConnectionString).Options;
        await using var ownerContext = new ApplicationDbContext(options, tenantContext);
        var migrator = ownerContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260929192919_OpenIddict", Ct);

        var userId = Guid.CreateVersion7();
        await ownerContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."AspNetUsers"
                ("Id", "Culture", "TimeZoneId", "Status", "IsPlatformOperator", "EmailConfirmed",
                 "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            VALUES ({userId}, 'es-AR', 'America/Argentina/Buenos_Aires', 'Active', false,
                    false, false, false, true, 0)
            """, Ct);
        var firstTenant = Tenant.CreateBusiness("Backfill A", requiresApproval: false);
        var secondTenant = Tenant.CreateBusiness("Backfill B", requiresApproval: false);
        Assert.True(firstTenant.Activate().IsSuccess);
        Assert.True(secondTenant.Activate().IsSuccess);
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ITenantRepository>().Add(firstTenant);
                services.GetRequiredService<ITenantRepository>().Add(secondTenant);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        var firstJoined = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        var secondJoined = firstJoined.AddDays(1);
        await SeedMemberAsync(isolated, userId, firstTenant.Id, firstJoined, inactive: false);
        await SeedMemberAsync(isolated, userId, secondTenant.Id, secondJoined, inactive: true);

        await migrator.MigrateAsync("20260929194130_UserTenantAccessIndex", Ct);

        await using var connection = new NpgsqlConnection(isolated.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT "TenantId", "Status", "JoinedAtUtc"
            FROM identity."UserTenantAccesses" WHERE "UserId" = @user_id
            """, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var indexed = new Dictionary<Guid, (string Status, DateTime JoinedAtUtc)>();
        while (await reader.ReadAsync(Ct))
            indexed.Add(reader.GetGuid(0), (reader.GetString(1), reader.GetDateTime(2)));

        Assert.Equal(2, indexed.Count);
        Assert.Equal(("Active", firstJoined), indexed[firstTenant.Id]);
        Assert.Equal(("Inactive", secondJoined), indexed[secondTenant.Id]);
    }

    private static async Task SeedMemberAsync(ApiFactory source, Guid userId, Guid tenantId,
        DateTime joinedAtUtc, bool inactive)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(source.RuntimeConnectionString, tenantId, Ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."Members" ("Id", "TenantId", "UserId", "Status", "JoinedAtUtc")
            VALUES (@id, @tenant_id, @user_id, @status, @joined_at)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("status", inactive ? "Inactive" : "Active");
        command.Parameters.AddWithValue("joined_at", joinedAtUtc);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    private async Task<(string Status, DateTime JoinedAtUtc)?> ReadIndexAsync(Guid userId, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT "Status", "JoinedAtUtc" FROM identity."UserTenantAccesses"
            WHERE "UserId" = @user_id AND "TenantId" = @tenant_id
            """, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        return await reader.ReadAsync(Ct) ? (reader.GetString(0), reader.GetDateTime(1)) : null;
    }

    private static async Task<string?> ReadIndexStatusAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, Guid tenantId)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "Status" FROM identity."UserTenantAccesses"
            WHERE "UserId" = @user_id AND "TenantId" = @tenant_id
            """, connection, transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return (string?)await command.ExecuteScalarAsync(Ct);
    }
}
