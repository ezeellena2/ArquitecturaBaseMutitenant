using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorPersistenceTests(ApiFactory factory)
{
    private const string BeforePermissions = "20261003221619_SecurityEventEvidence";
    private const string OperatorPermissions = "20261004005437_PlatformOperatorPermissions";
    private static readonly string[] HistoricalOwnerPermissions =
    [
        "platform.tenants.read", "platform.tenants.manage", "platform.accounts.read", "platform.accounts.manage",
        "platform.recoveries.manage", "platform.legal.manage", "platform.operators.manage", "platform.audit.read", "platform.settings.manage"
    ];
    private static readonly string[] HistoricalSupportPermissions =
    ["platform.tenants.read", "platform.accounts.read", "platform.recoveries.manage", "platform.audit.read"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_turns_owner_and_support_roles_into_their_effective_permissions_and_back()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient();
        var ownerId = await CreateAccountAsync(isolated.Services, "Operador anterior a R00");
        var supportId = await CreateAccountAsync(isolated.Services, "Soporte anterior a R00");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();

        // ADR 0042: el catálogo histórico de nueve permisos vuelve a Owner, aunque el catálogo vivo crezca.
        await using (var scope = isolated.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var repository = scope.ServiceProvider.GetRequiredService<IPlatformPermissionAssignmentRepository>();
                repository.Add(PlatformPermissionAssignment.Create(ownerId, HistoricalOwnerPermissions).Value);
                repository.Add(PlatformPermissionAssignment.Create(supportId, [PlatformPermissions.Audit.Read]).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        await migrator.MigrateAsync(BeforePermissions, Ct);
        Assert.Equal("Owner", await RoleAsync(isolated, ownerId));
        Assert.Equal("Support", await RoleAsync(isolated, supportId));

        await migrator.MigrateAsync(OperatorPermissions, Ct);

        var reader = isolated.Services.GetRequiredService<IPlatformPermissionAssignmentReader>();
        Assert.Equal(HistoricalOwnerPermissions, await reader.FindPermissionsAsync(ownerId, Ct));
        Assert.Equal(HistoricalSupportPermissions, await reader.FindPermissionsAsync(supportId, Ct));
        Assert.False(await context.Tenants.AnyAsync(Ct));
    }

    [Fact]
    public async Task Permissions_are_global_persisted_and_versioned_without_an_active_organization()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlatformPermissionAssignmentRepository>();
        var reader = services.GetRequiredService<IPlatformPermissionAssignmentReader>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var account = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Soporte de prueba", "es-AR", "America/Argentina/Buenos_Aires", ct);
            repository.Add(PlatformPermissionAssignment.Create(user.Id, PlatformOperators.Reduced).Value);
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(PlatformOperators.Reduced, await reader.FindPermissionsAsync(account.Value, Ct));
        Assert.Null(await reader.FindPermissionsAsync(Guid.CreateVersion7(), Ct));
        var context = services.GetRequiredService<ApplicationDbContext>();
        var originalVersion = await context.PlatformPermissionAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == account.Value).Select(assignment => assignment.Version).SingleAsync(Ct);
        Assert.NotEqual(0u, originalVersion);
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var assignment = await repository.GetByUserIdAsync(account.Value, ct);
            Assert.NotNull(assignment);
            return assignment.ChangePermissions(PlatformPermissions.All);
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(PlatformPermissions.All, await reader.FindPermissionsAsync(account.Value, Ct));
        var newVersion = await context.PlatformPermissionAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == account.Value).Select(assignment => assignment.Version).SingleAsync(Ct);
        Assert.NotEqual(originalVersion, newVersion);
    }

    [Fact]
    public async Task Repository_requires_a_transaction_and_database_rejects_duplicate_assignments()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlatformPermissionAssignmentRepository>();
        Assert.Throws<InvalidOperationException>(() => repository.Add(
            PlatformPermissionAssignment.Create(Guid.CreateVersion7(), PlatformPermissions.All).Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByUserIdAsync(Guid.CreateVersion7(), Ct));
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var created = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Operador de prueba", "es-AR", "America/Argentina/Buenos_Aires", ct);
            repository.Add(PlatformPermissionAssignment.Create(user.Id, PlatformPermissions.All).Value);
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        var duplicate = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            repository.Add(PlatformPermissionAssignment.Create(created.Value, PlatformOperators.Reduced).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct));
        Assert.Equal("IX_PlatformPermissionAssignments_UserId", duplicate.ConstraintName);
    }

    private static async Task<Guid> CreateAccountAsync(IServiceProvider provider, string name)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var account = await services.GetRequiredService<IUserRepository>()
                .CreateAsync(name, "es-AR", "America/Argentina/Buenos_Aires", ct);
            return Result.Success(account.Id);
        }, CommitPolicy.OnSuccess, Ct);
        return created.Value;
    }

    private static async Task<string?> RoleAsync(ApiFactory isolated, Guid userId)
    {
        await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT "Role" FROM platform."PlatformRoleAssignments" WHERE "UserId" = @userId
            """, connection);
        command.Parameters.AddWithValue("userId", userId);
        return (string?)await command.ExecuteScalarAsync(Ct);
    }
}
