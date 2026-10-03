using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformOperatorPersistenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_preserves_existing_operator_accounts_as_owner_without_creating_personal_spaces()
    {
        await using var legacyFactory = new ApiFactory();
        await legacyFactory.InitializeAsync();
        using var client = legacyFactory.CreateClient();
        Guid operatorId;
        await using (var scope = legacyFactory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var created = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var account = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
                    .CreateAsync("Operador anterior a E5", "es-AR", "America/Argentina/Buenos_Aires", ct);
                var identity = await context.Users.SingleAsync(user => user.Id == account.Id, ct);
                identity.GrantPlatformOperator();
                return Result.Success(account.Id);
            }, CommitPolicy.OnSuccess, Ct);
            operatorId = created.Value;
        }
        await using (var connection = new NpgsqlConnection(legacyFactory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                DROP TABLE platform."PlatformRoleAssignments";
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261003155300_PlatformOperatorRoles';
                """, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(legacyFactory.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var migrated = new ApplicationDbContext(options.Options, new TenantContext());
        await migrated.Database.MigrateAsync(Ct);

        var assignment = await migrated.PlatformRoleAssignments.SingleAsync(Ct);
        Assert.Equal(operatorId, assignment.UserId);
        Assert.Equal(PlatformRole.Owner, assignment.Role);
        Assert.Equal(1, await migrated.Users.CountAsync(Ct));
        Assert.False(await migrated.Tenants.AnyAsync(Ct));
    }

    [Fact]
    public async Task Roles_are_global_persisted_and_versioned_without_an_active_organization()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlatformRoleAssignmentRepository>();
        var reader = services.GetRequiredService<IPlatformRoleAssignmentReader>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var account = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Soporte de prueba", "es-AR", "America/Argentina/Buenos_Aires", ct);
            repository.Add(PlatformRoleAssignment.Create(user.Id, PlatformRole.Support));
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(PlatformRole.Support, await reader.FindRoleAsync(account.Value, Ct));
        var context = services.GetRequiredService<ApplicationDbContext>();
        var originalVersion = await context.PlatformRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == account.Value).Select(assignment => assignment.Version).SingleAsync(Ct);
        Assert.NotEqual(0u, originalVersion);
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var assignment = await repository.GetByUserIdAsync(account.Value, ct);
            Assert.NotNull(assignment);
            assignment.ChangeRole(PlatformRole.Owner);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        Assert.Equal(PlatformRole.Owner, await reader.FindRoleAsync(account.Value, Ct));
        var newVersion = await context.PlatformRoleAssignments.AsNoTracking()
            .Where(assignment => assignment.UserId == account.Value).Select(assignment => assignment.Version).SingleAsync(Ct);
        Assert.NotEqual(originalVersion, newVersion);
    }

    [Fact]
    public async Task Repository_requires_a_transaction_and_database_rejects_duplicate_assignments()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<IPlatformRoleAssignmentRepository>();
        Assert.Throws<InvalidOperationException>(() => repository.Add(PlatformRoleAssignment.Create(Guid.CreateVersion7(), PlatformRole.Owner)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetByUserIdAsync(Guid.CreateVersion7(), Ct));
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var created = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Operador de prueba", "es-AR", "America/Argentina/Buenos_Aires", ct);
            repository.Add(PlatformRoleAssignment.Create(user.Id, PlatformRole.Owner));
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        var duplicate = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            repository.Add(PlatformRoleAssignment.Create(created.Value, PlatformRole.Support));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct));
        Assert.Equal("IX_PlatformRoleAssignments_UserId", duplicate.ConstraintName);
    }
}
