using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Un permiso incluye a otro (decisión 260): guardar un rol guarda lo pedido más lo incluido; un rol viejo, guardado sin
/// lo incluido, igual lo permite (lo efectivo, también en la caché); y la migración completa una vez los roles de cada
/// organización y los permisos de los operadores, sin cruzar organizaciones y con su historia sin autor.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PermissionInclusionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_role_saves_what_it_includes_and_an_old_role_still_allows_it()
    {
        using var ownerClient = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Lo incluido", Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct, tenant);

        using var created = await AccountJourney.PostAsync(ownerClient, "/api/roles", new
        {
            name = "Personas",
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Users.Manage },
        }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var id = await created.Content.ReadFromJsonAsync<Guid>(Ct);
        var role = await ownerClient.GetFromJsonAsync<JsonElement>($"/api/roles/{id:D}", Ct);
        Assert.Equal([Permissions.Users.Manage, Permissions.Users.Read], Codes(role.GetProperty("permissions")));

        using var updated = await ownerClient.PutAsJsonAsync($"/api/roles/{id:D}", new
        {
            name = "Personas",
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = new[] { Permissions.Settings.Manage },
            version = role.GetProperty("version").GetUInt32(),
        }, Ct);
        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        role = await ownerClient.GetFromJsonAsync<JsonElement>($"/api/roles/{id:D}", Ct);
        Assert.Equal([Permissions.Settings.Manage, Permissions.Settings.Read], Codes(role.GetProperty("permissions")));

        // Un rol guardado por la versión anterior, con «Administrar usuarios» y sin «Ver usuarios»: igual lista usuarios.
        using var memberClient = Client(factory);
        var member = await AccountJourney.RegisterAsync(factory, memberClient, Ct);
        var memberId = await OrganizationJourney.AddActiveMemberAsync(factory, tenant, member.UserId, Ct);
        await AssignOldRoleAsync(factory, tenant, memberId, [Permissions.Users.Manage]);
        await AccountJourney.AuthorizeAsync(memberClient, "business", Ct, tenant);

        using var users = await memberClient.GetAsync("/api/users/paged", Ct);
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        using var roles = await memberClient.GetAsync("/api/roles/paged", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
        var me = await memberClient.GetFromJsonAsync<JsonElement>("/api/me", Ct);
        Assert.Equal([Permissions.Users.Manage, Permissions.Users.Read],
            Codes(me.GetProperty("effectivePermissions").GetProperty("organization")));
    }

    [Fact]
    public async Task The_migration_completes_roles_and_operators_once_without_crossing_organizations()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = Client(isolated);
        var owner = await AccountJourney.RegisterAsync(isolated, client, Ct);
        var first = await OrganizationJourney.StageAsync(isolated, owner, "Primera", Ct);
        var second = await OrganizationJourney.StageAsync(isolated, owner, "Segunda", Ct);
        var people = await AddOldRoleAsync(isolated, first, "Personas", RoleScope.Organization, [Permissions.Users.Manage]);
        var members = await AddOldRoleAsync(isolated, first, "Miembros", RoleScope.AnyCompany,
            [Permissions.Company.Members.Manage]);
        var complete = await AddOldRoleAsync(isolated, first, "Completo", RoleScope.Organization,
            [Permissions.Roles.Manage, Permissions.Roles.Read]);
        var settings = await AddOldRoleAsync(isolated, second, "Configuración", RoleScope.Organization,
            [Permissions.Settings.Manage, Permissions.Users.Invite]);
        using var operatorClient = Client(isolated);
        var operatorAccount = await AccountJourney.RegisterAsync(isolated, operatorClient, Ct);
        await PlatformOperators.GrantAsync(isolated.Services, operatorAccount.UserId,
            [PlatformPermissions.Accounts.Manage, PlatformPermissions.Audit.Read], Ct);
        // Un rol de sistema dañado no se repara acá: los de sistema ya tienen todo su catálogo.
        var owners = await DamageSystemRoleAsync(isolated, first, Permissions.Users.Read);
        var before = (First: await GenerationAsync(isolated, first), Second: await GenerationAsync(isolated, second));

        await MigrateAsync(isolated);

        var firstRoles = await RolesAsync(isolated, first);
        var secondRoles = await RolesAsync(isolated, second);
        Assert.Equal([Permissions.Users.Manage, Permissions.Users.Read], firstRoles[people]);
        Assert.Equal([Permissions.Company.Members.Manage, Permissions.Company.Members.Read], firstRoles[members]);
        Assert.Equal([Permissions.Roles.Manage, Permissions.Roles.Read], firstRoles[complete]);
        Assert.Equal([Permissions.Settings.Manage, Permissions.Settings.Read, Permissions.Users.Invite, Permissions.Users.Read],
            secondRoles[settings]);
        Assert.DoesNotContain(Permissions.Users.Read, firstRoles[owners]);
        Assert.Contains(Permissions.Users.Manage, firstRoles[owners]);
        // Cada organización conserva solo sus roles: los dos de sistema y los suyos.
        Assert.Equal(5, firstRoles.Count);
        Assert.Equal(3, secondRoles.Count);
        Assert.DoesNotContain(settings, firstRoles.Keys);
        Assert.Equal([PlatformPermissions.Accounts.Read, PlatformPermissions.Accounts.Manage, PlatformPermissions.Audit.Read],
            await OperatorAsync(isolated, operatorAccount.UserId));

        // La historia sin autor: un cambio por rol completado, en su organización, con lo que tenía y lo que tiene.
        var firstHistory = await SystemRoleChangesAsync(isolated, first);
        Assert.Equal(new[] { members, people }.Order(), firstHistory.Keys.Order());
        using (var change = JsonDocument.Parse(firstHistory[people]))
        {
            Assert.Equal([Permissions.Users.Manage], Codes(change.RootElement.GetProperty("Permissions").GetProperty("old")));
            Assert.Equal([Permissions.Users.Manage, Permissions.Users.Read],
                Codes(change.RootElement.GetProperty("Permissions").GetProperty("new")));
        }
        Assert.Equal([settings], (await SystemRoleChangesAsync(isolated, second)).Keys);
        var operatorEvent = await OperatorEventsAsync(isolated, operatorAccount.UserId);
        using (var change = JsonDocument.Parse(Assert.Single(operatorEvent)))
        {
            var property = Assert.Single(change.RootElement.EnumerateObject());
            Assert.Equal(PlatformPermissions.Accounts.Read, property.Name);
            Assert.False(property.Value.GetProperty("before").GetBoolean());
            Assert.True(property.Value.GetProperty("after").GetBoolean());
        }
        Assert.Equal(before.First + 1, await GenerationAsync(isolated, first));
        Assert.Equal(before.Second + 1, await GenerationAsync(isolated, second));

        // Una segunda corrida no cambia nada: ni permisos, ni historia, ni generaciones.
        await MigrateAsync(isolated);
        Assert.Equal(firstRoles, await RolesAsync(isolated, first));
        Assert.Equal(2, (await SystemRoleChangesAsync(isolated, first)).Count);
        Assert.Single(await OperatorEventsAsync(isolated, operatorAccount.UserId));
        Assert.Equal(before.First + 1, await GenerationAsync(isolated, first));
        Assert.Equal(before.Second + 1, await GenerationAsync(isolated, second));
    }

    private static HttpClient Client(ApiFactory target) =>
        target.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static string[] Codes(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];

    // Las operaciones de la migración tal como las aplica el despliegue, con el rol administrador.
    private static async Task MigrateAsync(ApiFactory target)
    {
        var operations = new CompleteIncludedPermissions().UpOperations.OfType<SqlOperation>().ToArray();
        Assert.Equal([CompleteIncludedPermissions.CompleteRolesSql, CompleteIncludedPermissions.CompleteOperatorsSql],
            operations.Select(operation => operation.Sql));
        await using var connection = new NpgsqlConnection(target.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        foreach (var operation in operations)
        {
            await using var command = new NpgsqlCommand(operation.Sql, connection, transaction);
            await command.ExecuteNonQueryAsync(Ct);
        }
        await transaction.CommitAsync(Ct);
    }

    // Saca un permiso del Dueño con SQL, por fuera del guardado, como en SystemRolePermissionUpgradeTests.
    private static async Task<Guid> DamageSystemRoleAsync(ApiFactory target, Guid tenantId, string permission)
    {
        await using var connection = new NpgsqlConnection(target.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, true)", connection, transaction))
        {
            scope.Parameters.AddWithValue("tenant", tenantId.ToString("D"));
            await scope.ExecuteScalarAsync(Ct);
        }
        await using var command = new NpgsqlCommand("""
            UPDATE tenant."Roles" SET "Permissions" = array_remove("Permissions", @permission)
            WHERE "IsSystemRole" AND "Name" = 'TenantAdmin'
            RETURNING "Id"
            """, connection, transaction);
        command.Parameters.AddWithValue("permission", permission);
        var id = (Guid)(await command.ExecuteScalarAsync(Ct))!;
        await transaction.CommitAsync(Ct);
        return id;
    }

    // Un rol como los que guardaba la versión anterior: lo pedido, sin lo incluido.
    private static async Task<Guid> AddOldRoleAsync(ApiFactory target, Guid tenantId, string name, RoleScope scope,
        string[] permissions)
    {
        var role = Role.Create(name, null, scope, null, permissions).Value;
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.ServiceProvider.GetRequiredService<IRoleRepository>().Add(role);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return role.Id;
    }

    private static async Task AssignOldRoleAsync(ApiFactory target, Guid tenantId, Guid memberId, string[] permissions)
    {
        var role = Role.Create("Viejo " + Guid.NewGuid().ToString("N")[..8], null, RoleScope.Organization, null,
            permissions).Value;
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.ServiceProvider.GetRequiredService<ApplicationDbContext>().AddRange(role,
                RoleAssignment.Assign(memberId, role.Id, role.Scope, null, null).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<Dictionary<Guid, string[]>> RolesAsync(ApiFactory target, Guid tenantId)
    {
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var roles = await services.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.AsNoTracking()
            .ToArrayAsync(Ct);
        // Sin ordenar: la migración deja el orden en que guarda RoleService, así guardar sin cambios no deja historia.
        return roles.ToDictionary(role => role.Id, role => role.Permissions.ToArray());
    }

    private static async Task<Dictionary<Guid, string>> SystemRoleChangesAsync(ApiFactory target, Guid tenantId)
    {
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var entries = await services.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>()
            .AsNoTracking()
            .Where(entry => entry.ActorKind == AuditActorKind.System && entry.ActorId == null
                && entry.Action == AuditAction.Updated && entry.EntityType == nameof(Role))
            .ToArrayAsync(Ct);
        Assert.All(entries, entry => Assert.Equal(tenantId, entry.TenantId));
        return entries.ToDictionary(entry => entry.EntityId, entry => entry.Changes);
    }

    private static async Task<string[]> OperatorAsync(ApiFactory target, Guid userId)
    {
        await using var services = target.Services.CreateAsyncScope();
        var assignment = await services.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .PlatformPermissionAssignments.AsNoTracking().SingleAsync(item => item.UserId == userId, Ct);
        // Sin ordenar: la migración deja el orden del catálogo, el mismo en que guarda PlatformPermissionAssignment.
        return [.. assignment.Permissions];
    }

    private static async Task<string[]> OperatorEventsAsync(ApiFactory target, Guid userId)
    {
        await using var services = target.Services.CreateAsyncScope();
        return await services.ServiceProvider.GetRequiredService<ApplicationDbContext>().SecurityEvents.AsNoTracking()
            .Where(item => item.Type == SecurityEventType.PlatformOperatorPermissionsChanged
                && item.TargetAccountId == userId && item.ActorKind == AuditActorKind.System)
            .Select(item => item.Changes).ToArrayAsync(Ct);
    }

    private static async Task<long> GenerationAsync(ApiFactory target, Guid tenantId)
    {
        await using var services = target.Services.CreateAsyncScope();
        var tag = CacheTags.Roles(tenantId);
        return await services.ServiceProvider.GetRequiredService<ApplicationDbContext>().CacheTagVersions.AsNoTracking()
            .Where(version => version.Tag == tag).Select(version => version.Generation).SingleOrDefaultAsync(Ct);
    }
}
