using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// La migración del nombre del perfil (C14 de la E12): el Up suma el nombre cifrado del mensaje y la marca del contacto; el
/// Down vuelve atrás sin ninguno de los dos y, con un contacto que ya lo tomó o un mensaje que lo trae, se niega antes que
/// borrarlos. La guarda corre como la migración, con el dueño de la tabla y sin organización, y la RLS queda forzada.
/// </summary>
public sealed class WhatsAppContactProfileNameMigrationTests
{
    [Fact]
    public async Task Down_goes_back_without_profile_names_and_refuses_to_lose_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Perfil " + Guid.NewGuid().ToString("N")[..8], ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261127123346_FormCatalogs";
        const string current = "20261127131006_WhatsAppContactProfileName";
        string[] columns = ["identity.WhatsAppMessages.EncryptedProfileName", "tenant.WhatsAppCustomers.ProfileNameTakenAtUtc"];

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await migrator.MigrateAsync(current, ct);
        Assert.Equal(columns, await ColumnsAsync());

        // Un contacto que ya tomó el nombre de su perfil, visible solo dentro de su organización.
        Assert.Equal(1, await RunAsync("""
            INSERT INTO tenant."WhatsAppCustomers" ("TenantId", "Id", "WaId", "Tags", "Consent", "CreatedAtUtc", "ProfileNameTakenAtUtc")
            VALUES (@tenant, gen_random_uuid(), '+5491134567890', '{}', 'NoAnswer', now(), now())
            """, inTenant: true));
        Assert.Equal(0, await RunAsync("""UPDATE tenant."WhatsAppCustomers" SET "Name" = NULL""", inTenant: false));
        await RefusedAsync();

        // Sin la marca, un mensaje que todavía trae el nombre cifrado también se niega.
        Assert.Equal(1, await RunAsync("""UPDATE tenant."WhatsAppCustomers" SET "ProfileNameTakenAtUtc" = NULL""", inTenant: true));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppMessages" ("Id", "PhoneNumberId", "Wamid", "EventType", "Direction", "Kind", "Status",
                "ReceivedAtUtc", "OccurredAtUtc", "Attempts", "ContentType", "EncryptedProfileName")
            VALUES (gen_random_uuid(), 'migration-number', 'wamid.migration', 'inbound', 'Inbound', 'Unsupported', 'Pending',
                now(), now(), 0, 'image', 'ciphertext')
            """, inTenant: false));
        await RefusedAsync();

        Assert.Equal(1, await RunAsync("""UPDATE identity."WhatsAppMessages" SET "EncryptedProfileName" = NULL WHERE "Wamid" = 'wamid.migration'""", inTenant: false));
        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await migrator.MigrateAsync(current, ct);
        Assert.False(db.Database.HasPendingModelChanges());

        async Task RefusedAsync()
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
            Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
            Assert.Equal(columns, await ColumnsAsync());
            Assert.True(await RlsForcedAsync());
            Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
        }

        async Task<string[]> ColumnsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT table_schema || '.' || table_name || '.' || column_name FROM information_schema.columns
                WHERE (table_schema, table_name, column_name) IN
                    (('identity', 'WhatsAppMessages', 'EncryptedProfileName'), ('tenant', 'WhatsAppCustomers', 'ProfileNameTakenAtUtc'))
                ORDER BY 1
                """, connection);
            var found = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) found.Add(reader.GetString(0));
            return [.. found];
        }

        async Task<bool> RlsForcedAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"WhatsAppCustomers\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        // El dueño también pasa por la RLS forzada: para ver o tocar el contacto entra a su organización, como la app.
        async Task<int> RunAsync(string sql, bool inTenant)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            if (inTenant)
            {
                await using var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection);
                enter.Parameters.AddWithValue("tenant", tenantId.ToString());
                await enter.ExecuteNonQueryAsync(ct);
            }
            await using var command = new NpgsqlCommand(sql, connection);
            if (sql.Contains("@tenant", StringComparison.Ordinal)) command.Parameters.AddWithValue("tenant", tenantId);
            return await command.ExecuteNonQueryAsync(ct);
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
