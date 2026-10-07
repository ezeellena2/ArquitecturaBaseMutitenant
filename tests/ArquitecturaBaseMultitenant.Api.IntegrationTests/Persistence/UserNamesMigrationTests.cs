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

/// <summary>Actualiza identidades existentes sin convertir el rótulo anónimo en nombre personal.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class UserNamesMigrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_splits_first_word_and_remainder_keeps_nulls_and_skips_deleted_accounts()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient();
        var names = new string?[] { null, "", "Ana", "María José de la Cruz", "Cuenta eliminada" };
        var ids = new List<Guid>();
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                foreach (var _ in names)
                    ids.Add((await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006235613_RemoveAccountReauthentication", Ct);
        for (var index = 0; index < ids.Count; index++)
        {
            var status = index == ids.Count - 1 ? "Deleted" : "Active";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE identity.\"AspNetUsers\" SET \"DisplayName\"={names[index]}, \"Status\"={status} WHERE \"Id\"={ids[index]}", Ct);
        }
        await migrator.MigrateAsync(cancellationToken: Ct);
        await using var connection = new NpgsqlConnection(isolated.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        for (var index = 0; index < ids.Count; index++)
        {
            await using var command = new NpgsqlCommand("""
                SELECT "FirstName", "LastName", "DisplayName", "ProfileVersion"
                FROM identity."AspNetUsers" WHERE "Id"=@id
                """, connection);
            command.Parameters.AddWithValue("id", ids[index]);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            Assert.True(await reader.ReadAsync(Ct));
            var first = index switch { 2 => "Ana", 3 => "María", _ => null };
            var last = index == 3 ? "José de la Cruz" : null;
            Assert.Equal(first, reader.IsDBNull(0) ? null : reader.GetString(0));
            Assert.Equal(last, reader.IsDBNull(1) ? null : reader.GetString(1));
            Assert.Equal(index == 1 ? null : names[index], reader.IsDBNull(2) ? null : reader.GetString(2));
            Assert.Equal(0, reader.GetInt64(3));
        }
    }
}
