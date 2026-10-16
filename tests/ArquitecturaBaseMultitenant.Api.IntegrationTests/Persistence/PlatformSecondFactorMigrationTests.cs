using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// La migración del segundo factor (E9): ninguna sesión de Plataforma anterior sigue valiendo, tampoco la del
/// operador del seed. Revoca las autorizaciones y tokens de Plataforma, renueva el sello de los operadores y no toca
/// las sesiones públicas ni a las personas.
/// </summary>
public sealed class PlatformSecondFactorMigrationTests
{
    [Fact]
    public async Task Pre_e9_platform_sessions_are_revoked_and_operators_get_a_new_stamp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, migration => migration.EndsWith("_PlatformSecondFactor", StringComparison.Ordinal));
        Assert.True(index > 0);
        await migrator.MigrateAsync(cancellationToken: ct);

        // Un operador y una persona, cada uno con su autorización y su token como los dejó el ingreso anterior.
        Guid operatorId = Guid.Empty, personId = Guid.Empty;
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                operatorId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", work)).Id;
                personId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", work)).Id;
                await users.SetPlatformOperatorAsync(operatorId, true, work);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var platformAuthorization = await AuthorizationAsync(db, operatorId, "platform", ct);
        var consumerAuthorization = await AuthorizationAsync(db, personId, "consumer", ct);
        var stamps = await StampsAsync(db, operatorId, personId, ct);

        await migrator.MigrateAsync(migrations[index - 1], ct);
        await migrator.MigrateAsync(migrations[index], ct);

        Assert.Equal("revoked", await StatusAsync(db, "OpenIddictAuthorizations", platformAuthorization, ct));
        Assert.Equal("revoked", await StatusAsync(db, "OpenIddictTokens", platformAuthorization, ct));
        Assert.Equal("valid", await StatusAsync(db, "OpenIddictAuthorizations", consumerAuthorization, ct));
        Assert.Equal("valid", await StatusAsync(db, "OpenIddictTokens", consumerAuthorization, ct));
        var after = await StampsAsync(db, operatorId, personId, ct);
        Assert.NotEqual(stamps.Operator, after.Operator);
        Assert.Equal(stamps.Person, after.Person);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<Guid> AuthorizationAsync(ApplicationDbContext db, Guid userId, string access, CancellationToken ct)
    {
        var authorizationId = Guid.CreateVersion7();
        var properties = $$"""{"access":"{{access}}","session_id":"browser"}""";
        var subject = userId.ToString("D");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO platform."OpenIddictAuthorizations" ("Id", "Properties", "Status", "Subject", "Type")
            VALUES ({authorizationId}, {properties}, 'valid', {subject}, 'ad-hoc');
            INSERT INTO platform."OpenIddictTokens" ("Id", "AuthorizationId", "Status", "Subject", "Type")
            VALUES ({Guid.CreateVersion7()}, {authorizationId}, 'valid', {subject}, 'refresh_token');
            """, ct);
        return authorizationId;
    }

    private static Task<string> StatusAsync(ApplicationDbContext db, string table, Guid authorizationId, CancellationToken ct) =>
        table == "OpenIddictTokens"
            ? db.Database.SqlQuery<string>($"""SELECT "Status" AS "Value" FROM platform."OpenIddictTokens" WHERE "AuthorizationId" = {authorizationId}""").SingleAsync(ct)
            : db.Database.SqlQuery<string>($"""SELECT "Status" AS "Value" FROM platform."OpenIddictAuthorizations" WHERE "Id" = {authorizationId}""").SingleAsync(ct);

    private static async Task<(string Operator, string Person)> StampsAsync(ApplicationDbContext db, Guid operatorId, Guid personId,
        CancellationToken ct)
    {
        var stamps = await db.Users.AsNoTracking().Where(user => user.Id == operatorId || user.Id == personId)
            .Select(user => new { user.Id, user.SecurityStamp }).ToArrayAsync(ct);
        return (stamps.Single(row => row.Id == operatorId).SecurityStamp!, stamps.Single(row => row.Id == personId).SecurityStamp!);
    }
}
