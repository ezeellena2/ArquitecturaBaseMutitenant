using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

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
