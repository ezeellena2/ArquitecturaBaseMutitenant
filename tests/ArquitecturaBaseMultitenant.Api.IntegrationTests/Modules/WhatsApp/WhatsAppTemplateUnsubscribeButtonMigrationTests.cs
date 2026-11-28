using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// KAN-45: las plantillas guardadas antes del cambio conservan si se crearon con el botón «No recibir más» (true las
/// Marketing, false las Utility), respetando la RLS forzada de <c>tenant."WhatsAppTemplates"</c>, y una recategorización
/// posterior en <c>WhatsAppTemplateStates</c> no cambia ese valor.
/// </summary>
public sealed class WhatsAppTemplateUnsubscribeButtonMigrationTests
{
    [Fact]
    public async Task Existing_templates_keep_their_creation_unsubscribe_button_across_up_down_and_recategorization()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Plantillas " + Guid.NewGuid().ToString("N")[..8], ct);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, name => name.EndsWith("_WhatsAppTemplateUnsubscribeButton", StringComparison.Ordinal));
        Assert.True(index > 0);
        var previous = migrations[index - 1];
        var current = migrations[index];
        var migrator = db.GetService<IMigrator>();
        string[] columns = ["platform.PlatformWhatsAppTemplates.HasUnsubscribeButton", "tenant.WhatsAppTemplates.HasUnsubscribeButton"];

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());

        var marketingId = Guid.CreateVersion7();
        var utilityId = Guid.CreateVersion7();
        var platformUtilityId = Guid.CreateVersion7();

        Assert.Equal(1, await RunAsync("""
            INSERT INTO tenant."WhatsAppTemplates" ("Id", "TenantId", "Variables", "Name", "MetaName", "Language", "MetaLanguage", "Category", "Body", "CreatedAtUtc")
            VALUES (@id, @tenant, '[]'::jsonb, 'Promo previa', 'promo_previa', 'es-AR', 'es_AR', 'Marketing', 'Promo previa', now())
            """, inTenant: true, marketingId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO tenant."WhatsAppTemplates" ("Id", "TenantId", "Variables", "Name", "MetaName", "Language", "MetaLanguage", "Category", "Body", "CreatedAtUtc")
            VALUES (@id, @tenant, '[]'::jsonb, 'Aviso previo', 'aviso_previo', 'es-AR', 'es_AR', 'Utility', 'Aviso previo', now())
            """, inTenant: true, utilityId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO platform."PlatformWhatsAppTemplates" ("Id", "Variables", "Name", "MetaName", "Language", "MetaLanguage", "Category", "Body", "CreatedAtUtc")
            VALUES (@id, '[]'::jsonb, 'Consulta previa', 'consulta_previa', 'es-AR', 'es_AR', 'Utility', 'Consulta previa', now())
            """, inTenant: false, platformUtilityId));

        await migrator.MigrateAsync(current, ct);
        Assert.Equal(columns, await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await AssertFlagsAsync();

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());

        await migrator.MigrateAsync(current, ct);
        Assert.Equal(columns, await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await AssertFlagsAsync();

        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.False(db.Database.HasPendingModelChanges());

        // Meta recategoriza la Utility a Marketing y la Marketing a Utility: el botón sigue lo guardado al crearse.
        var wabaId = "waba-migration-" + Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppChannels" ("Id", "OrganizationId", "PhoneNumberId", "WabaId", "Status", "Coexistence", "Source",
                "ConsultationsEnabled", "UsageTimeZoneId", "EncryptedToken", "ConnectedAtUtc", "CreatedAtUtc")
            VALUES (@id, @tenant, @waba, @waba, 'Connected', false, 'Signup', false, 'America/Argentina/Buenos_Aires', '\x01'::bytea, now(), now())
            """, inTenant: false, Guid.CreateVersion7(), wabaId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppTemplateStates" ("Id", "WabaId", "MetaName", "MetaLanguage", "Status", "Category", "UpdatedAtUtc")
            VALUES (@id, @waba, 'aviso_previo', 'es_AR', 'Approved', 'Marketing', now())
            """, inTenant: false, Guid.CreateVersion7(), wabaId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppTemplateStates" ("Id", "WabaId", "MetaName", "MetaLanguage", "Status", "Category", "UpdatedAtUtc")
            VALUES (@id, @waba, 'promo_previa', 'es_AR', 'Approved', 'Utility', now())
            """, inTenant: false, Guid.CreateVersion7(), wabaId));

        await using var scope = isolated.Services.CreateAsyncScope();
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId))
        {
            var reader = scope.ServiceProvider.GetRequiredService<IWhatsAppTemplateReader>();
            var recategorizedUtility = await reader.FindAsync(platform: false, utilityId, ct);
            Assert.NotNull(recategorizedUtility);
            Assert.Equal((WhatsAppTemplateCategory.Marketing, false), (recategorizedUtility.Category, recategorizedUtility.HasUnsubscribeButton));

            var recategorizedMarketing = await reader.FindAsync(platform: false, marketingId, ct);
            Assert.NotNull(recategorizedMarketing);
            Assert.Equal((WhatsAppTemplateCategory.Utility, true), (recategorizedMarketing.Category, recategorizedMarketing.HasUnsubscribeButton));
        }

        async Task AssertFlagsAsync()
        {
            Assert.True(await FlagAsync("tenant.\"WhatsAppTemplates\"", marketingId, inTenant: true));
            Assert.False(await FlagAsync("tenant.\"WhatsAppTemplates\"", utilityId, inTenant: true));
            Assert.False(await FlagAsync("platform.\"PlatformWhatsAppTemplates\"", platformUtilityId, inTenant: false));
        }

        async Task<bool> FlagAsync(string table, Guid id, bool inTenant)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            if (inTenant)
            {
                await using var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection);
                enter.Parameters.AddWithValue("tenant", tenantId.ToString());
                await enter.ExecuteNonQueryAsync(ct);
            }
            await using var command = new NpgsqlCommand($"SELECT \"HasUnsubscribeButton\" FROM {table} WHERE \"Id\" = @id", connection);
            command.Parameters.AddWithValue("id", id);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<string[]> ColumnsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT table_schema || '.' || table_name || '.' || column_name FROM information_schema.columns
                WHERE (table_schema, table_name, column_name) IN
                    (('tenant', 'WhatsAppTemplates', 'HasUnsubscribeButton'), ('platform', 'PlatformWhatsAppTemplates', 'HasUnsubscribeButton'))
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
                "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"WhatsAppTemplates\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<int> RunAsync(string sql, bool inTenant, Guid id, string? waba = null)
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
            command.Parameters.AddWithValue("id", id);
            if (sql.Contains("@tenant", StringComparison.Ordinal)) command.Parameters.AddWithValue("tenant", tenantId);
            if (waba is not null) command.Parameters.AddWithValue("waba", waba);
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
