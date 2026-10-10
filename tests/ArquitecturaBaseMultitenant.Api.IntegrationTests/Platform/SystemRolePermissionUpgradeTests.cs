using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Upgrade real de plantillas persistidas, sin abrir una edición de roles de sistema.</summary>
public sealed class SystemRolePermissionUpgradeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
    });

    [Fact]
    public async Task Legacy_business_roles_upgrade_under_forced_rls_and_approve_retry_preserve_the_space()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var pending = await CreateSpaceAsync(factory, owner.UserId, true);
        var active = await CreateSpaceAsync(factory, owner.UserId, false);
        var personal = Tenant.CreatePersonal("Espacio personal sin upgrade");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(personal.Id);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ITenantRepository>().Add(personal);
                services.GetRequiredService<IRoleRepository>().Add(SystemRoles.CreateTenantAdmin());
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct)).IsSuccess);
        }
        foreach (var id in new[] { pending.Id, active.Id, personal.Id })
            await SqlAsync(factory, id, """
                UPDATE tenant."Roles" SET "Permissions" = array_remove(array_remove("Permissions", 'assistant.use'), 'assistant.configure')
                WHERE "IsSystemRole";
                """);
        // Una instalación parcialmente actualizada conserva tokens preexistentes sin duplicarlos.
        await SqlAsync(factory, active.Id, """
            UPDATE tenant."Roles" SET "Permissions" = array_append("Permissions", 'assistant.use') WHERE "IsSystemRole";
            """);
        var beforePending = await SnapshotAsync(factory, pending.Id);
        var beforeActive = await SnapshotAsync(factory, active.Id);
        var beforePersonal = await SnapshotAsync(factory, personal.Id);

        using var operatorClient = Client(factory);
        var operatorAccount = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        foreach (var (space, action) in new[] { (pending, "approve"), (active, "retry-provisioning") })
        {
            using var legacy = await PlatformJourney.PostAsync(operatorClient, $"/api/platform/tenants/{space.Id:D}/{action}",
                new { version = await VersionAsync(operatorClient, space.Id), reason = "Comprobar plantilla anterior" }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, legacy.StatusCode);
            using var problem = JsonDocument.Parse(await legacy.Content.ReadAsStringAsync(Ct));
            Assert.Equal(TenantErrors.ProvisioningConflictCode, problem.RootElement.GetProperty("code").GetString());
        }

        await using var db = AdminContext(factory);
        var (previous, current) = MigrationPair(db);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(previous, Ct);
        // Un scope de conexión previo debe sobrevivir al loop, incluso si corresponde a Personal.
        await db.Database.OpenConnectionAsync(Ct);
        await db.Database.ExecuteSqlAsync($"SELECT set_config('app.tenant_id', {personal.Id.ToString("D")}, false);", Ct);
        await AssertOwnerAndRlsAsync(db);
        await migrator.MigrateAsync(current, Ct);
        Assert.Equal(personal.Id.ToString("D"), await ScalarAsync(db, "SELECT current_setting('app.tenant_id', true);"));
        await AssertUpgradedAsync(factory, pending.Id, beforePending);
        await AssertUpgradedAsync(factory, active.Id, beforeActive);
        Assert.Equal(beforePersonal, await SnapshotAsync(factory, personal.Id));
        await migrator.MigrateAsync(previous, Ct);
        await AssertUpgradedAsync(factory, pending.Id, beforePending);
        await AssertUpgradedAsync(factory, active.Id, beforeActive);
        await migrator.MigrateAsync(current, Ct);
        await AssertUpgradedAsync(factory, pending.Id, beforePending);
        await AssertUpgradedAsync(factory, active.Id, beforeActive);
        await migrator.MigrateAsync(cancellationToken: Ct);
        Assert.False(db.Database.HasPendingModelChanges());

        foreach (var (space, action) in new[] { (pending, "approve"), (active, "retry-provisioning") })
        {
            var version = await VersionAsync(operatorClient, space.Id);
            using var result = await PlatformJourney.PostAsync(operatorClient, $"/api/platform/tenants/{space.Id:D}/{action}",
                new { version, reason = "Comprobar upgrade de permisos" }, Ct);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var savedVersion = await SavedVersion.ReadAsync(result, Ct);
            using var repeated = await PlatformJourney.PostAsync(operatorClient, $"/api/platform/tenants/{space.Id:D}/retry-provisioning",
                new { version = savedVersion }, Ct);
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            using var stale = await PlatformJourney.PostAsync(operatorClient, $"/api/platform/tenants/{space.Id:D}/retry-provisioning",
                new { version }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            await AssertUpgradedAsync(factory, space.Id, space == pending ? beforePending : beforeActive);
        }
        await using var check = factory.Services.CreateAsyncScope();
        Assert.All(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tenants
            .AsNoTracking().Where(t => t.Id == pending.Id || t.Id == active.Id).ToArrayAsync(Ct),
            t => Assert.Equal(TenantStatus.Active, t.Status));
    }

    [Fact]
    public async Task Legacy_cache_is_ignored_current_cache_is_invalidated_and_other_tenants_are_isolated()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var first = await CreateSpaceAsync(factory, owner.UserId, false);
        var other = await CreateSpaceAsync(factory, owner.UserId, false);
        var roles = await RolesAsync(factory, first.Id);
        var roleId = roles.Single(r => r.Name == SystemRoles.TenantAdmin).Id;
        var cache = factory.Services.GetRequiredService<HybridCache>();
        var permissionCache = factory.Services.GetRequiredService<IRolePermissionCache>();
        var legacyKey = CacheKeys.Tenant(first.Id, "role-permissions:" + roleId.ToString("N"));
        await cache.SetAsync(legacyKey, new[] { Permissions.Roles.Read }, cancellationToken: Ct);
        Assert.Equal(Permissions.All.Order(StringComparer.Ordinal),
            await permissionCache.GetRolePermissionsAsync(first.Id, roleId, Ct));
        Assert.Empty(await permissionCache.GetRolePermissionsAsync(other.Id, roleId, Ct));
        await SqlAsync(factory, first.Id, """
            UPDATE tenant."Roles" SET "Permissions" = array_remove("Permissions", 'assistant.configure')
            WHERE "Name" = 'TenantAdmin';
            """);
        Assert.Contains(Permissions.Assistant.Configure, await permissionCache.GetRolePermissionsAsync(first.Id, roleId, Ct));
        await permissionCache.InvalidateRoleAsync(first.Id, roleId, Ct);
        Assert.DoesNotContain(Permissions.Assistant.Configure, await permissionCache.GetRolePermissionsAsync(first.Id, roleId, Ct));
        Assert.Equal(new[] { Permissions.Roles.Read }, await cache.GetOrCreateAsync(legacyKey,
            _ => ValueTask.FromResult(Array.Empty<string>()), cancellationToken: Ct));
        Assert.Empty(await permissionCache.GetRolePermissionsAsync(other.Id, roleId, Ct));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("scope")]
    [InlineData("company")]
    [InlineData("custom")]
    public async Task Upgrade_does_not_repair_unrelated_corruption_or_roles_matched_only_by_name(string corruption)
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var ownerClient = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var space = await CreateSpaceAsync(factory, owner.UserId, false);
        await SqlAsync(factory, space.Id, """
            UPDATE tenant."Roles" SET "Permissions" = array_remove(array_remove("Permissions", 'assistant.use'), 'assistant.configure')
            WHERE "IsSystemRole";
            """);
        var sql = corruption switch
        {
            "missing" => "UPDATE tenant.\"Roles\" SET \"Permissions\" = array_remove(\"Permissions\", 'roles.read') WHERE \"Name\"='TenantAdmin';",
            "duplicate" => "UPDATE tenant.\"Roles\" SET \"Permissions\" = array_append(\"Permissions\", 'roles.read') WHERE \"Name\"='TenantAdmin';",
            "scope" => "UPDATE tenant.\"Roles\" SET \"Scope\"='AnyCompany' WHERE \"Name\"='TenantAdmin';",
            "company" => "UPDATE tenant.\"Roles\" SET \"CompanyId\"=(SELECT \"Id\" FROM tenant.\"Companies\" LIMIT 1) WHERE \"Name\"='TenantAdmin';",
            _ => "UPDATE tenant.\"Roles\" SET \"IsSystemRole\"=false WHERE \"Name\"='TenantAdmin';",
        };
        await SqlAsync(factory, space.Id, sql);
        var before = await RolesAsync(factory, space.Id);
        await using var db = AdminContext(factory);
        var (previous, current) = MigrationPair(db);
        await db.GetService<IMigrator>().MigrateAsync(previous, Ct);
        await db.GetService<IMigrator>().MigrateAsync(current, Ct);
        var after = await RolesAsync(factory, space.Id);
        var damaged = before.Single(r => r.Name == SystemRoles.TenantAdmin);
        var saved = after.Single(r => r.Name == SystemRoles.TenantAdmin);
        Assert.Equal(damaged.Id, saved.Id);
        Assert.Equal(damaged.Scope, saved.Scope);
        Assert.Equal(damaged.CompanyId, saved.CompanyId);
        Assert.Equal(damaged.IsSystemRole, saved.IsSystemRole);
        if (corruption is "scope" or "company" or "custom") Assert.Equal(damaged.Permissions, saved.Permissions);
        if (corruption == "missing") Assert.DoesNotContain(Permissions.Roles.Read, saved.Permissions);
        if (corruption == "duplicate") Assert.Equal(2, saved.Permissions.Count(p => p == Permissions.Roles.Read));
        using var operatorClient = Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, operatorClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using var result = await PlatformJourney.PostAsync(operatorClient, $"/api/platform/tenants/{space.Id:D}/retry-provisioning",
            new { version = await VersionAsync(operatorClient, space.Id) }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantErrors.ProvisioningConflictCode, json.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Tenant> CreateSpaceAsync(ApiFactory factory, Guid ownerId, bool pending)
    {
        var space = Tenant.CreateBusiness("Upgrade " + Guid.NewGuid().ToString("N")[..8], pending);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(space.Id);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            Assert.True(space.RecordRegistration(ownerId, TenantRegistrationSource.SelfService,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            var company = Company.Create("Primera empresa", null, "UTC").Value;
            services.GetRequiredService<ICompanyRepository>().Add(company);
            var staged = await services.GetRequiredService<TenantProvisioner>().StageAsync(space,
                TenantSettings.Create("es-AR", "UTC", "ARS"), [ownerId], ownerId, ct, company.Id);
            if (staged.IsFailure) return staged;
            var custom = Role.Create("Rol personalizado", "Conservar", RoleScope.Organization, null,
                [Permissions.Roles.Read, Permissions.Roles.Read]).Value;
            services.GetRequiredService<IRoleRepository>().Add(custom);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await db.Roles.CountAsync(Ct));
        Assert.Equal(2, await db.RoleAssignments.CountAsync(Ct));
        Assert.Equal(1, await db.Members.CountAsync(Ct));
        Assert.Equal(1, await db.Companies.CountAsync(Ct));
        Assert.Equal(1, await db.CompanyMemberships.CountAsync(Ct));
        Assert.Equal(1, await db.TenantSettings.CountAsync(Ct));
        return space;
    }

    private static ApplicationDbContext AdminContext(ApiFactory factory)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(factory.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        return new ApplicationDbContext(options.Options, new TenantContext());
    }

    private static (string Previous, string Current) MigrationPair(ApplicationDbContext db)
    {
        var migrations = db.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, m => m.EndsWith("_SystemRoleAssistantPermissions", StringComparison.Ordinal));
        Assert.True(index > 0);
        return (migrations[index - 1], migrations[index]);
    }

    private static async Task SqlAsync(ApiFactory factory, Guid tenantId, string sql)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, tenantId, Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<uint> VersionAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/platform/tenants/{id:D}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    private static async Task<RoleRow[]> RolesAsync(ApiFactory factory, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var roles = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.AsNoTracking()
            .OrderBy(r => r.Name).ToArrayAsync(Ct);
        return roles.Select(r => new RoleRow(r.Id, r.Name, r.Scope, r.CompanyId, r.IsSystemRole, [.. r.Permissions])).ToArray();
    }

    private static async Task<string> SnapshotAsync(ApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(id);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return JsonSerializer.Serialize(new
        {
            Roles = await RolesAsync(factory, id),
            Assignments = await db.RoleAssignments.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(Ct),
            Members = await db.Members.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(Ct),
            Companies = await db.Companies.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(Ct),
            Memberships = await db.CompanyMemberships.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(Ct),
            Settings = await db.TenantSettings.AsNoTracking().ToArrayAsync(Ct),
        });
    }

    private static async Task AssertUpgradedAsync(ApiFactory factory, Guid id, string before)
    {
        using var expected = JsonDocument.Parse(before);
        using var actual = JsonDocument.Parse(await SnapshotAsync(factory, id));
        foreach (var field in new[] { "Assignments", "Members", "Companies", "Memberships", "Settings" })
            Assert.Equal(expected.RootElement.GetProperty(field).GetRawText(), actual.RootElement.GetProperty(field).GetRawText());
        var roles = await RolesAsync(factory, id);
        var originals = expected.RootElement.GetProperty("Roles").Deserialize<RoleRow[]>()!;
        Assert.Equal(originals.Length, roles.Length);
        foreach (var role in roles)
        {
            var old = originals.Single(r => r.Id == role.Id);
            Assert.Equal(old.Name, role.Name);
            Assert.Equal(old.Scope, role.Scope);
            Assert.Equal(old.CompanyId, role.CompanyId);
            Assert.Equal(old.IsSystemRole, role.IsSystemRole);
            var appended = role.Name == SystemRoles.TenantAdmin
                ? new[] { Permissions.Assistant.Use, Permissions.Assistant.Configure }
                : role.Name == SystemRoles.CompanyAdmin ? new[] { Permissions.Assistant.Use } : [];
            Assert.Equal(old.Permissions.Concat(appended.Where(p => !old.Permissions.Contains(p))), role.Permissions);
            if (role.Name == SystemRoles.CompanyAdmin) Assert.DoesNotContain(Permissions.Assistant.Configure, role.Permissions);
        }
    }

    private static async Task AssertOwnerAndRlsAsync(ApplicationDbContext db)
    {
        Assert.Equal("mt_owner", await ScalarAsync(db, "SELECT current_user;"));
        Assert.Equal("false:false", await ScalarAsync(db,
            "SELECT rolsuper::text || ':' || rolbypassrls::text FROM pg_roles WHERE rolname=current_user;"));
        Assert.Equal("true:true", await ScalarAsync(db,
            "SELECT relrowsecurity::text || ':' || relforcerowsecurity::text FROM pg_class WHERE oid='tenant.\"Roles\"'::regclass;"));
    }

    private static async Task<string> ScalarAsync(ApplicationDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed record RoleRow(Guid Id, string Name, RoleScope Scope, Guid? CompanyId, bool IsSystemRole, string[] Permissions);
}
