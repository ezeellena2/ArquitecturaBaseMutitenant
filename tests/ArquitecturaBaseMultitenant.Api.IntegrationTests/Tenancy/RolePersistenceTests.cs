using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Verifica en PostgreSQL real el aislamiento, las referencias y xmin de los roles.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class RolePersistenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Role_rows_are_isolated_and_reader_projects_permissions()
    {
        var firstTenant = await CreateBusinessTenantAsync("Organización de roles A");
        var otherTenant = await CreateBusinessTenantAsync("Organización de roles B");
        var role = Role.Create("Lector", "Lee la administración", RoleScope.Organization, null,
            [Permissions.Roles.Read, Permissions.Users.Read]).Value;
        await AddRoleAsync(firstTenant.Id, role);

        await using var first = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString,
            firstTenant.Id, Ct);
        await using var other = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString,
            otherTenant.Id, Ct);
        Assert.Equal(1, await VisibleRoleCountAsync(first, role.Id));
        Assert.Equal(0, await VisibleRoleCountAsync(other, role.Id));

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(firstTenant.Id);
        var reader = services.GetRequiredService<IRoleReader>();
        var row = Assert.Single(await reader.ListAllAsync(Ct));
        Assert.Equal(role.Id, row.Id);
        Assert.Equal([Permissions.Roles.Read, Permissions.Users.Read], row.Permissions);
        var details = await reader.FindByIdAsync(role.Id, Ct);
        Assert.Equal(row.Id, details?.Id);
        Assert.Equal(row.Permissions, details?.Permissions);
        Assert.True(await reader.ExistsByNameAsync(" lector ", null, Ct));
    }

    [Fact]
    public async Task Assignment_rejects_a_member_missing_from_the_same_tenant()
    {
        var tenant = await CreateBusinessTenantAsync("Organización con rol huérfano");
        var role = Role.Create("Lector", null, RoleScope.Organization, null, []).Value;
        await AddRoleAsync(tenant.Id, role);
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString,
            tenant.Id, Ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO tenant."RoleAssignments" ("Id", "TenantId", "MemberId", "RoleId", "CreatedAtUtc")
            VALUES (@id, @tenant_id, @member_id, @role_id, @created_at_utc)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("tenant_id", tenant.Id);
        command.Parameters.AddWithValue("member_id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("role_id", role.Id);
        command.Parameters.AddWithValue("created_at_utc",
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.NotNull(error.ConstraintName);
        Assert.Contains("RoleAssignments_Members", error.ConstraintName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Concurrent_role_updates_reject_a_stale_xmin()
    {
        var tenant = await CreateBusinessTenantAsync("Organización con concurrencia");
        var role = Role.Create("Versión inicial", null, RoleScope.Organization, null, []).Value;
        await AddRoleAsync(tenant.Id, role);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        using var firstTenant = firstScope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        using var secondTenant = secondScope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        var firstServices = firstScope.ServiceProvider;
        var secondServices = secondScope.ServiceProvider;
        var firstRole = await firstServices.GetRequiredService<ApplicationDbContext>().Roles
            .SingleAsync(item => item.Id == role.Id, Ct);
        var staleRole = await secondServices.GetRequiredService<ApplicationDbContext>().Roles
            .SingleAsync(item => item.Id == role.Id, Ct);

        Assert.Equal(firstRole.Version, staleRole.Version);
        Assert.True(firstRole.Update("Versión nueva", null, RoleScope.Organization, null, []).IsSuccess);
        await firstServices.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, Ct);

        Assert.True(staleRole.Update("Versión obsoleta", null, RoleScope.Organization, null, []).IsSuccess);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            secondServices.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, Ct));
    }

    private async Task<Tenant> CreateBusinessTenantAsync(string name)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task AddRoleAsync(Guid tenantId, Role role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<IRoleRepository>().Add(role);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<int> VisibleRoleCountAsync(NpgsqlConnection connection, Guid roleId)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM tenant."Roles" WHERE "Id" = @role_id
            """, connection);
        command.Parameters.AddWithValue("role_id", roleId);
        return checked((int)(long)(await command.ExecuteScalarAsync(Ct))!);
    }
}
