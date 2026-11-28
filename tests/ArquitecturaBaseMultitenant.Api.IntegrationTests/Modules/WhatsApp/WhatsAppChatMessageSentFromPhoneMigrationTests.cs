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
/// KAN-44 (B10): los mensajes de chat guardados antes del cambio quedan con <c>SentFromPhone = false</c> al migrar
/// adelante, atrás y adelante, respetando la RLS forzada de <c>tenant."WhatsAppChatMessages"</c>; con un eco guardado, el
/// Down se niega.
/// </summary>
public sealed class WhatsAppChatMessageSentFromPhoneMigrationTests
{
    [Fact]
    public async Task Existing_chat_messages_keep_sent_from_phone_false_across_up_and_down()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Ecos " + Guid.NewGuid().ToString("N")[..8], ct);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, name => name.EndsWith("_WhatsAppChatMessageSentFromPhone", StringComparison.Ordinal));
        Assert.True(index > 0);
        var previous = migrations[index - 1];
        var current = migrations[index];
        var migrator = db.GetService<IMigrator>();
        string[] columns = ["platform.PlatformWhatsAppChatMessages.SentFromPhone", "tenant.WhatsAppChatMessages.SentFromPhone"];

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());

        var orgChannelId = Guid.CreateVersion7();
        var platformChannelId = Guid.CreateVersion7();
        var orgChatId = Guid.CreateVersion7();
        var platformChatId = Guid.CreateVersion7();
        var orgMessageId = Guid.CreateVersion7();
        var platformMessageId = Guid.CreateVersion7();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppChannels" ("Id", "OrganizationId", "PhoneNumberId", "WabaId", "Status", "Coexistence", "Source",
                "ConsultationsEnabled", "UsageTimeZoneId", "EncryptedToken", "ConnectedAtUtc", "CreatedAtUtc")
            VALUES (@id, @tenant, @number, @waba, 'Connected', true, 'Signup', false, 'America/Argentina/Buenos_Aires', '\x01'::bytea, now(), now())
            """, inTenant: false, orgChannelId, number: "pn-org-" + suffix, waba: "waba-org-" + suffix));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO identity."WhatsAppChannels" ("Id", "OrganizationId", "PhoneNumberId", "WabaId", "Status", "Coexistence", "Source",
                "ConsultationsEnabled", "UsageTimeZoneId", "EncryptedToken", "ConnectedAtUtc", "CreatedAtUtc")
            VALUES (@id, NULL, @number, @waba, 'Connected', false, 'Signup', true, 'America/Argentina/Buenos_Aires', '\x01'::bytea, now(), now())
            """, inTenant: false, platformChannelId, number: "pn-plat-" + suffix, waba: "waba-plat-" + suffix));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO tenant."WhatsAppChats" ("Id", "TenantId", "ChannelId", "Inbox", "WaId", "StartedAtUtc", "LastMessageAtUtc", "AwaitingReply")
            VALUES (@id, @tenant, @parent, 'Customers', '+5491134567890', now(), now(), true)
            """, inTenant: true, orgChatId, parent: orgChannelId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO tenant."WhatsAppChatMessages" ("Id", "TenantId", "ChatId", "Kind", "Direction", "Status", "CreatedAtUtc")
            VALUES (@id, @tenant, @parent, 'Text', 'Outbound', 'Sent', now())
            """, inTenant: true, orgMessageId, parent: orgChatId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO platform."PlatformWhatsAppChats" ("Id", "ChannelId", "WaId", "StartedAtUtc", "LastMessageAtUtc", "AwaitingReply")
            VALUES (@id, @parent, '+5491134567891', now(), now(), true)
            """, inTenant: false, platformChatId, parent: platformChannelId));
        Assert.Equal(1, await RunAsync("""
            INSERT INTO platform."PlatformWhatsAppChatMessages" ("Id", "ChatId", "Kind", "Direction", "Status", "CreatedAtUtc")
            VALUES (@id, @parent, 'Text', 'Outbound', 'Sent', now())
            """, inTenant: false, platformMessageId, parent: platformChatId));

        await migrator.MigrateAsync(current, ct);
        Assert.Equal(columns, await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await AssertFlagsFalseAsync();

        // Con un eco del celular guardado, en cualquiera de las dos tablas, el Down se niega y deja todo como estaba.
        foreach (var (table, id, inTenant) in new[]
        {
            ("tenant.\"WhatsAppChatMessages\"", orgMessageId, true),
            ("platform.\"PlatformWhatsAppChatMessages\"", platformMessageId, false),
        })
        {
            Assert.Equal(1, await RunAsync($"UPDATE {table} SET \"SentFromPhone\" = true WHERE \"Id\" = @id", inTenant, id));
            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
            Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
            Assert.Equal(columns, await ColumnsAsync());
            Assert.True(await RlsForcedAsync());
            Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
            Assert.True(await FlagAsync(table, id, inTenant));
            Assert.Equal(1, await RunAsync($"UPDATE {table} SET \"SentFromPhone\" = false WHERE \"Id\" = @id", inTenant, id));
        }

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ColumnsAsync());
        Assert.True(await RlsForcedAsync());

        await migrator.MigrateAsync(current, ct);
        Assert.Equal(columns, await ColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await AssertFlagsFalseAsync();

        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.False(db.Database.HasPendingModelChanges());

        await using var scope = isolated.Services.CreateAsyncScope();
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId))
        {
            var reader = scope.ServiceProvider.GetRequiredService<IWhatsAppConversationReader>();
            var orgMessage = await reader.FindMessageAsync(WhatsAppInbox.Customers, orgChatId, orgMessageId, ct);
            Assert.NotNull(orgMessage);
            Assert.False(orgMessage.SentFromPhone);

            var platformMessage = await reader.FindMessageAsync(null, platformChatId, platformMessageId, ct);
            Assert.NotNull(platformMessage);
            Assert.False(platformMessage.SentFromPhone);
        }

        async Task AssertFlagsFalseAsync()
        {
            Assert.False(await FlagAsync("tenant.\"WhatsAppChatMessages\"", orgMessageId, inTenant: true));
            Assert.False(await FlagAsync("platform.\"PlatformWhatsAppChatMessages\"", platformMessageId, inTenant: false));
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
            await using var command = new NpgsqlCommand($"SELECT \"SentFromPhone\" FROM {table} WHERE \"Id\" = @id", connection);
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
                    (('tenant', 'WhatsAppChatMessages', 'SentFromPhone'), ('platform', 'PlatformWhatsAppChatMessages', 'SentFromPhone'))
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
                "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"WhatsAppChatMessages\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<int> RunAsync(string sql, bool inTenant, Guid id, Guid? parent = null, string? number = null, string? waba = null)
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
            if (parent is { } parentId) command.Parameters.AddWithValue("parent", parentId);
            if (number is not null) command.Parameters.AddWithValue("number", number);
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
