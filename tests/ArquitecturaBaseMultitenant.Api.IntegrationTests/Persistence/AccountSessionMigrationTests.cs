using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>Actualiza datos anteriores a A5 conservando sólo la prueba de cancelar la baja.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountSessionMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_removes_old_codes_preserves_cancel_proofs_and_allows_runtime_persistence()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient();
        Guid userId;
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            userId = (await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                Result.Success((await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id), CommitPolicy.OnSuccess, Ct)).Value;
        }
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261005221817_PlatformTenantDetailDates", Ct);
        var proofId = Guid.CreateVersion7();
        var now = isolated.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."ReauthTickets" ("Id","UserId","Action","TokenHash","IssuedAtUtc","ExpiresAtUtc","ReturnUrl")
            VALUES ({proofId},{userId},'CancelDeletion','cancel-hash',{now},{now.AddMinutes(5)},'/invitacion'),
                   ({Guid.CreateVersion7()},{userId},'AddEmail','obsolete-hash',{now},{now.AddMinutes(5)},NULL);
            INSERT INTO identity."LoginCodes" ("Id","Destination","Channel","CodeHash","Purpose","CreatedAtUtc","ExpiresAtUtc","FailedAttempts","MaxAttempts")
            VALUES ({Guid.CreateVersion7()},'old@example.test','email','hash','Reauthenticate',{now},{now.AddMinutes(5)},0,5),
                   ({Guid.CreateVersion7()},'login@example.test','email','hash','Login',{now},{now.AddMinutes(5)},0,5);
            """, Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);
        await using var connection = new NpgsqlConnection(isolated.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT "Id", "ReturnUrl", "ExpiresAtUtc" - "IssuedAtUtc" FROM identity."AccountDeletionCancelTickets";
            SELECT "Purpose" FROM identity."LoginCodes";
            SELECT has_table_privilege(current_user, 'identity."AccountDeletionCancelTickets"', 'INSERT,UPDATE,DELETE');
            SELECT to_regclass('identity."ReauthTickets"'), count(*) FROM information_schema.columns
              WHERE table_schema='identity' AND table_name='LoginCodes' AND column_name IN ('ReauthAction','SourceMethodId','TargetMethodId');
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(proofId, reader.GetGuid(0));
        Assert.Equal("/invitacion", reader.GetString(1));
        Assert.Equal(TimeSpan.FromMinutes(5), reader.GetTimeSpan(2));
        Assert.False(await reader.ReadAsync(Ct));
        Assert.True(await reader.NextResultAsync(Ct));
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal("Login", reader.GetString(0));
        Assert.False(await reader.ReadAsync(Ct));
        Assert.True(await reader.NextResultAsync(Ct));
        Assert.True(await reader.ReadAsync(Ct));
        Assert.True(reader.GetBoolean(0));
        Assert.True(await reader.NextResultAsync(Ct));
        Assert.True(await reader.ReadAsync(Ct));
        Assert.True(reader.IsDBNull(0));
        Assert.Equal(0, reader.GetInt64(1));
    }
}
