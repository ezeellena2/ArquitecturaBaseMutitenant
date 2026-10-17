using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>Upgrade de datos de gestores existentes; no vuelve a conceder permisos desde el seed.</summary>
public sealed class PlatformOperatorAssistantUpgradeTests
{
    private const string Upgrade = "20261010214712_PlatformOperatorAssistantPermissions";
    private const string Reason = "Platform operator assistant permissions upgrade";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Added =
        [PlatformPermissions.Assistant.Use, PlatformPermissions.Assistant.Configure, PlatformPermissions.Assistant.Read];
    // Lo que sumaron a los gestores los upgrades posteriores de otros módulos instalados (en la E11, WhatsApp); llega al
    // migrar hasta la última migración. Sin esos módulos no queda nada: su retiro también lo saca.
    private static readonly string[] LaterUpgrades = [.. PlatformPermissions.All.Except(PlatformPermissions.LegacyBootstrap).Except(Added)];

    [Fact]
    public async Task Upgrade_appends_only_missing_permissions_preserves_accounts_and_audits_once_per_changed_assignment()
    {
        Assert.Equal(new[]
        {
            "platform.tenants.read", "platform.tenants.manage", "platform.accounts.read", "platform.accounts.manage",
            "platform.recoveries.manage", "platform.legal.manage", "platform.operators.manage", "platform.audit.read", "platform.settings.manage",
        }, PlatformPermissions.LegacyBootstrap);
        Assert.Equal(PlatformPermissions.LegacyBootstrap.Concat(Added).Concat(LaterUpgrades), PlatformPermissions.All);
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            using var client = AssistantTestData.Client(factory);
            var account = await AccountJourney.RegisterAsync(factory, client, Ct);
            ids.Add(account.UserId);
        }
        var grants = new string[][]
        {
            [.. PlatformPermissions.LegacyBootstrap],
            [PlatformPermissions.Accounts.Read],
            [PlatformPermissions.Operators.Manage, Added[0]],
            [PlatformPermissions.Operators.Manage, Added[1], Added[2]],
            [PlatformPermissions.Operators.Manage, .. Added],
            [PlatformPermissions.Operators.Manage, PlatformPermissions.Audit.Read],
            [PlatformPermissions.Operators.Manage],
        };
        for (var i = 0; i < grants.Length; i++)
            await PlatformOperators.GrantAsync(factory.Services, ids[i], grants[i], Ct);
        await SqlAsync(factory, "UPDATE identity.\"AspNetUsers\" SET \"IsPlatformOperator\"=true WHERE \"Id\"=@id", ids[7]);
        // Estados de cuenta e invitación no se cambian ni condicionan la selección de la migración.
        await SqlAsync(factory, "UPDATE identity.\"AspNetUsers\" SET \"Status\"='Suspended' WHERE \"Id\"=@id", ids[5]);
        await SqlAsync(factory, "UPDATE identity.\"AspNetUsers\" SET \"OperatorInvitationExpiresAtUtc\"=CURRENT_TIMESTAMP + interval '7 days' WHERE \"Id\"=@id", ids[6]);
        // Corrupción ajena deliberada: preservar orden y duplicados, no normalizar el array completo.
        await SqlAsync(factory, "UPDATE platform.\"PlatformPermissionAssignments\" SET \"Permissions\"=ARRAY['platform.audit.read','platform.operators.manage','platform.audit.read'] WHERE \"UserId\"=@id", ids[5]);
        var before = await SnapshotAsync(factory);
        await using var db = AdminContext(factory);
        var previous = Previous(db);
        await db.GetService<IMigrator>().MigrateAsync(previous, Ct);
        await db.Database.OpenConnectionAsync(Ct);
        Assert.Equal("mt_owner:false:false", await ScalarAsync(db,
            "SELECT current_user || ':' || rolsuper::text || ':' || rolbypassrls::text FROM pg_roles WHERE rolname=current_user"));
        await db.GetService<IMigrator>().MigrateAsync(Upgrade, Ct);
        var once = await SnapshotAsync(factory);
        AssertUpgrade(before, once);
        await AssertEventsAsync(factory, before, once);
        // Direct Up repetido, además de EF history: prueba el predicado de datos, no sólo el migrator.
        var sql = db.GetService<IMigrator>().GenerateScript(previous, Upgrade);
        var end = sql.IndexOf("INSERT INTO \"__EFMigrationsHistory\"", StringComparison.Ordinal);
        Assert.True(end > 0);
        await using (var repeated = db.Database.GetDbConnection().CreateCommand())
        {
            // Sólo SQL generado por EF desde las migraciones del assembly; sin entrada del usuario.
            repeated.CommandText = sql[..end] + "COMMIT;";
            await repeated.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(JsonSerializer.Serialize(once), JsonSerializer.Serialize(await SnapshotAsync(factory)));
        await AssertEventsAsync(factory, before, once);
        await db.GetService<IMigrator>().MigrateAsync(previous, Ct);
        Assert.Equal(JsonSerializer.Serialize(once), JsonSerializer.Serialize(await SnapshotAsync(factory)));
        await db.GetService<IMigrator>().MigrateAsync(Upgrade, Ct);
        await AssertEventsAsync(factory, before, once);
        Assert.Equal(JsonSerializer.Serialize(once), JsonSerializer.Serialize(await SnapshotAsync(factory)));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("true:true:false:false", await ScalarAsync(db, """
            SELECT has_table_privilege('mt_app','platform."PlatformPermissionAssignments"','SELECT,INSERT,UPDATE,DELETE')::text
                || ':' || EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='platform."PlatformPermissionAssignments"'::regclass AND contype='f')::text
                || ':' || has_table_privilege('mt_app','platform."SecurityEvents"','UPDATE')::text
                || ':' || has_table_privilege('mt_app','platform."SecurityEvents"','DELETE')::text
            """));
    }

    [Fact]
    public async Task Audit_insert_failure_rolls_back_grants_and_migration_history()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformPermissions.LegacyBootstrap, Ct);
        var before = await SnapshotAsync(factory);
        await using var db = AdminContext(factory);
        await db.GetService<IMigrator>().MigrateAsync(Previous(db), Ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION platform.reject_upgrade_evidence_test() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Reason" = 'Platform operator assistant permissions upgrade' THEN
                    RAISE EXCEPTION 'Evidence write rejected by test' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_upgrade_evidence_test BEFORE INSERT ON platform."SecurityEvents"
                FOR EACH ROW EXECUTE FUNCTION platform.reject_upgrade_evidence_test();
            """, Ct);
        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(Upgrade, Ct));
            Assert.Equal("23514", failure.SqlState);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await SnapshotAsync(factory)));
            Assert.DoesNotContain(Upgrade, await db.Database.GetAppliedMigrationsAsync(Ct));
            await AssertEventsAsync(factory, before, before);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER reject_upgrade_evidence_test ON platform."SecurityEvents";
                DROP FUNCTION platform.reject_upgrade_evidence_test();
                """, Ct);
        }
        await db.GetService<IMigrator>().MigrateAsync(Upgrade, Ct);
        var after = await SnapshotAsync(factory);
        AssertUpgrade(before, after);
        await AssertEventsAsync(factory, before, after);
    }

    [Fact]
    public async Task Legacy_manager_can_configure_after_upgrade_and_individual_revocation_survives_seed_and_restart()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var manager = AssistantTestData.Client(factory);
        var account = await ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns.AssistantTurnTestSession.PlatformAccountAsync(factory, manager);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformPermissions.LegacyBootstrap, Ct);
        await AccountJourney.AuthorizeAsync(manager, "platform", Ct);
        foreach (var path in new[] { "/api/platform/assistant/models", "/api/platform/assistant/settings", "/api/platform/assistant/conversations", "/api/platform/assistant/usage?month=2026-10" })
        {
            using var denied = await manager.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await using (var db = AdminContext(factory))
        {
            await db.GetService<IMigrator>().MigrateAsync(Previous(db), Ct);
            await db.GetService<IMigrator>().MigrateAsync(Upgrade, Ct);
            var upgraded = await db.PlatformPermissionAssignments.AsNoTracking().SingleAsync(value => value.UserId == account.UserId, Ct);
            Assert.Equal(PlatformPermissions.LegacyBootstrap.Concat(Added).Order(), upgraded.Permissions.Order());
            // El upgrade histórico se verifica con su esquema; el runtime exige luego el esquema actual completo.
            await db.GetService<IMigrator>().MigrateAsync(null, Ct);
        }
        using (var models = await manager.GetAsync("/api/platform/assistant/models", Ct))
            Assert.Equal(HttpStatusCode.OK, models.StatusCode);
        using (var history = await manager.GetAsync("/api/platform/assistant/conversations", Ct))
        {
            // El permiso ya pasa; aceptar el aviso sigue siendo una acción explícita de la cuenta.
            Assert.Equal(HttpStatusCode.BadRequest, history.StatusCode);
            using var problem = JsonDocument.Parse(await history.Content.ReadAsStringAsync(Ct));
            Assert.Equal("Assistant.Notice.AcceptanceRequired", problem.RootElement.GetProperty("code").GetString());
        }
        await AssistantExecutionActorTests.AcceptNoticeAsync(manager, "/api/platform/assistant/notice");
        using (var history = await manager.GetAsync("/api/platform/assistant/conversations", Ct))
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using (var usage = await manager.GetAsync("/api/platform/assistant/usage?month=2026-10", Ct))
            Assert.Equal(HttpStatusCode.OK, usage.StatusCode);
        using var settings = await ReadAsync(manager, "/api/platform/assistant/settings");
        Assert.False(settings.RootElement.GetProperty("hasKey").GetBoolean());
        using (var saved = await manager.PutAsJsonAsync("/api/platform/assistant/settings", new
        {
            defaultModelId = settings.RootElement.GetProperty("defaultModelId").GetGuid(),
            allowedModelIds = settings.RootElement.GetProperty("allowedModelIds").EnumerateArray().Select(v => v.GetGuid()).ToArray(),
            effort = "low", dailyMessageLimit = 50, monthlyOrganizationBudget = new { amount = 100m, currency = "USD" },
            personalEnabled = true, retentionDays = 30, keyAction = "keep", version = settings.RootElement.GetProperty("version").GetUInt32(),
        }, Ct))
            await SavedVersion.ReadAsync(saved, Ct);
        using (var saved = await ReadAsync(manager, "/api/platform/assistant/settings"))
            Assert.True(saved.RootElement.GetProperty("personalEnabled").GetBoolean());
        using var detail = await ReadAsync(manager, $"/api/platform/accounts/{account.UserId:D}");
        using (var own = await ChangeAsync(manager, account.UserId, detail, PlatformPermissions.LegacyBootstrap))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
            using var problem = JsonDocument.Parse(await own.Content.ReadAsStringAsync(Ct));
            Assert.Equal(UserErrors.CannotModifySelfCode, problem.RootElement.GetProperty("code").GetString());
        }
        using var admin = AssistantTestData.Client(factory);
        var other = await AccountJourney.RegisterAsync(factory, admin, Ct);
        await PlatformOperators.GrantAsync(factory.Services, other.UserId, PlatformPermissions.All, Ct);
        await AccountJourney.AuthorizeAsync(admin, "platform", Ct);
        foreach (var permission in Added)
        {
            using var current = await ReadAsync(admin, $"/api/platform/accounts/{account.UserId:D}");
            var remaining = current.RootElement.GetProperty("operatorPermissions").EnumerateArray()
                .Select(value => value.GetString()!).Where(value => value != permission).ToArray();
            using var revoked = await ChangeAsync(admin, account.UserId, current, remaining);
            await SavedVersion.ReadAsync(revoked, Ct);
            var path = permission == Added[0] ? "/api/platform/assistant/conversations"
                : permission == Added[1] ? "/api/platform/assistant/settings" : "/api/platform/assistant/usage?month=2026-10";
            using var denied = await manager.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        await factory.Services.SeedDatabaseAsync(Ct);
        using var restart = factory.WithWebHostBuilder(_ => { });
        await restart.Services.SeedDatabaseAsync(Ct);
        using var scope = restart.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PlatformPermissionAssignments
            .AsNoTracking().SingleAsync(value => value.UserId == account.UserId, Ct);
        Assert.Equal(PlatformPermissions.LegacyBootstrap.Concat(LaterUpgrades), row.Permissions);
    }

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, Guid id, JsonDocument detail, IEnumerable<string> permissions)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/platform/accounts/{id:D}/operator")
        {
            Content = JsonContent.Create(new { version = detail.RootElement.GetProperty("operatorVersion").GetUInt32(), reason = "Revisar permisos del upgrade", permissions }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static ApplicationDbContext AdminContext(ApiFactory factory)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(factory.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        return new ApplicationDbContext(options.Options, new TenantContext());
    }

    private static string Previous(ApplicationDbContext db)
    {
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.IndexOf(migrations, Upgrade);
        Assert.True(index > 0);
        return migrations[index - 1];
    }

    private static async Task SqlAsync(ApiFactory factory, string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<string> ScalarAsync(ApplicationDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task<Snapshot> SnapshotAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.PlatformPermissionAssignments.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(Ct);
        var users = await db.Users.AsNoTracking().OrderBy(user => user.Id)
            .Select(user => new { user.Id, user.Status, user.IsPlatformOperator, user.OperatorInvitationExpiresAtUtc }).ToArrayAsync(Ct);
        return new(rows.Select(row => new Assignment(row.Id, row.UserId, [.. row.Permissions])).ToArray(), JsonSerializer.Serialize(users));
    }

    private static void AssertUpgrade(Snapshot before, Snapshot after)
    {
        Assert.Equal(before.Users, after.Users);
        Assert.Equal(before.Rows.Length, after.Rows.Length);
        foreach (var old in before.Rows)
        {
            var saved = Assert.Single(after.Rows, row => row.Id == old.Id);
            Assert.Equal(old.UserId, saved.UserId);
            var missing = old.Permissions.Contains(PlatformPermissions.Operators.Manage)
                ? Added.Where(permission => !old.Permissions.Contains(permission)) : [];
            Assert.Equal(old.Permissions.Concat(missing), saved.Permissions);
        }
    }

    private static async Task AssertEventsAsync(ApiFactory factory, Snapshot before, Snapshot after)
    {
        using var scope = factory.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
            .Where(row => row.Reason == Reason).ToArrayAsync(Ct);
        var changed = before.Rows.Where(row => !row.Permissions.SequenceEqual(after.Rows.Single(saved => saved.Id == row.Id).Permissions)).ToArray();
        Assert.Equal(changed.Length, events.Length);
        foreach (var row in changed)
        {
            var ev = Assert.Single(events, ev => ev.TargetAccountId == row.UserId);
            Assert.Equal(SecurityEventType.PlatformOperatorPermissionsChanged, ev.Type);
            Assert.Equal(AuditActorKind.System, ev.ActorKind);
            Assert.Equal(Guid.Empty, ev.ActorId);
            Assert.Null(ev.TargetTenantId);
            Assert.Null(ev.MaskedIpAddress);
            Assert.Equal(DateTimeKind.Utc, ev.OccurredAtUtc.Kind);
            using var changes = JsonDocument.Parse(ev.Changes);
            var missing = Added.Where(permission => !row.Permissions.Contains(permission)).ToArray();
            Assert.Equal(missing.Order(), changes.RootElement.EnumerateObject().Select(property => property.Name).Order());
            foreach (var permission in missing)
            {
                Assert.False(changes.RootElement.GetProperty(permission).GetProperty("before").GetBoolean());
                Assert.True(changes.RootElement.GetProperty(permission).GetProperty("after").GetBoolean());
            }
        }
    }

    private sealed record Assignment(Guid Id, Guid UserId, string[] Permissions);
    private sealed record Snapshot(Assignment[] Rows, string Users);
}
