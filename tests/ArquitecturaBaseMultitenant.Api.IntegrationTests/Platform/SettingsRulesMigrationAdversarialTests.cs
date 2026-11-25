using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// Revisión adversarial de la B18: el <c>Down</c> de <c>SettingsRules</c> vuelve atrás sin reglas guardadas y se niega antes de
/// perder una, como pide la guía de migración («un Down que perdería datos se niega antes de cualquier DDL»): un plazo de
/// retención o el nombre de los correos de Plataforma, la configuración de WhatsApp de la plataforma o el plazo de los chats de
/// una organización, que la RLS forzada no esconde de la guarda. Cada negativa deja todo como estaba y la RLS forzada.
/// </summary>
public sealed class SettingsRulesMigrationAdversarialTests
{
    private const string Previous = "20261125130752_WhatsAppSortIndexes";
    private const string Current = "20261125153522_SettingsRules";

    [Fact]
    public async Task Down_goes_back_without_saved_rules_and_refuses_to_drop_rules_saved_in_settings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Reglas B18 " + Guid.NewGuid().ToString("N")[..8], ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Current, ct);

        // Sin reglas guardadas (los plazos de siempre y nada más) vuelve y va otra vez.
        await migrator.MigrateAsync(Previous, ct);
        Assert.False(await ExistsAsync("SELECT to_regclass('platform.\"WhatsAppPlatformSettings\"') IS NOT NULL"));
        await migrator.MigrateAsync(Current, ct);
        Assert.True(await RlsForcedAsync());

        // Una organización guardó el plazo de sus chats: el dueño, sin organización, no lo ve, y la guarda igual se niega.
        await RunAsync(tenantId, """
            INSERT INTO tenant."WhatsAppOrganizationSettings" ("Id", "TenantId", "ChatRetentionDays", "CreatedAtUtc")
            VALUES (gen_random_uuid(), @tenant::uuid, 30, now())
            """);
        Assert.False(await ExistsAsync("""SELECT EXISTS (SELECT 1 FROM tenant."WhatsAppOrganizationSettings")"""));
        await RefusedAsync();
        await RunAsync(tenantId, """DELETE FROM tenant."WhatsAppOrganizationSettings" """);

        // Plataforma guardó 24 meses de ingresos.
        await RunAsync(null, """UPDATE platform."PlatformSettings" SET "LoginAuditRetentionMonths" = 24""", expected: 1);
        await RefusedAsync();
        await RunAsync(null, """UPDATE platform."PlatformSettings" SET "LoginAuditRetentionMonths" = 12""", expected: 1);

        // Plataforma quitó el logo de los correos: el vacío también es una regla guardada.
        await RunAsync(null, """UPDATE platform."PlatformSettings" SET "EmailLogoUrl" = ''""", expected: 1);
        await RefusedAsync();
        await RunAsync(null, """UPDATE platform."PlatformSettings" SET "EmailLogoUrl" = NULL""", expected: 1);

        // Plataforma guardó los países de WhatsApp.
        await RunAsync(null, """
            INSERT INTO platform."WhatsAppPlatformSettings" ("Id", "AllowedCountries", "ChatRetentionDays", "MaxOrganizationChatRetentionDays", "CreatedAtUtc")
            VALUES ('00000000-0000-0000-0000-00000000a9a2', '["AR"]'::jsonb, 30, 365, now())
            """, expected: 1);
        await RefusedAsync();

        async Task RefusedAsync()
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Previous, ct));
            Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
            Assert.Contains(Current, await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.True(await ExistsAsync("SELECT to_regclass('platform.\"WhatsAppPlatformSettings\"') IS NOT NULL"));
            Assert.True(await RlsForcedAsync());
        }

        async Task<bool> RlsForcedAsync() =>
            await ExistsAsync("SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"WhatsAppOrganizationSettings\"'::regclass");

        async Task<bool> ExistsAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(sql, connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        // El dueño también pasa por la RLS forzada: para tocar la fila de la organización entra a ella, como la app.
        async Task RunAsync(Guid? tenant, string sql, int? expected = null)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
            {
                enter.Parameters.AddWithValue("tenant", tenant?.ToString() ?? string.Empty);
                await enter.ExecuteNonQueryAsync(ct);
            }
            await using var command = new NpgsqlCommand(sql, connection);
            if (tenant is { } id) command.Parameters.AddWithValue("tenant", id.ToString());
            var rows = await command.ExecuteNonQueryAsync(ct);
            if (expected is { } count) Assert.Equal(count, rows);
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
