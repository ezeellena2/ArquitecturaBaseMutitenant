using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// Revisión adversarial de la B16 (decisión 260): lo que deja la migración y lo que guarda la caché compartida.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class PermissionInclusionAdversarialTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // La migración agrega lo incluido al final del arreglo, y RoleService guarda en orden ordinal: guardar sin cambios un
    // rol completado no puede dejar en la historia un cambio de permisos a nombre de quien no cambió nada.
    [Fact]
    public async Task Saving_a_completed_role_without_changes_leaves_no_history_in_the_editors_name()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = Client(isolated);
        var owner = await AccountJourney.RegisterAsync(isolated, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(isolated, owner, "Sin cambios", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        var id = await AddOldRoleAsync(isolated, tenant, "Configuración",
            [Permissions.Settings.Manage, Permissions.Users.Invite]);

        await MigrateAsync(isolated);

        var role = await client.GetFromJsonAsync<JsonElement>($"/api/roles/{id:D}", Ct);
        using var saved = await client.PutAsJsonAsync($"/api/roles/{id:D}", new
        {
            name = role.GetProperty("name").GetString(),
            description = (string?)null,
            scope = "Organization",
            companyId = (Guid?)null,
            permissions = Codes(role.GetProperty("permissions")),
            version = role.GetProperty("version").GetUInt32(),
        }, Ct);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Ct));

        var edits = await UserEditsAsync(isolated, tenant, id);
        Assert.True(edits.Length == 0,
            "Saving without changes wrote a permissions change in the editor's name: " + string.Join(" | ", edits));
    }

    // En cada despliegue dos versiones comparten Redis (ADR 0060). Si una copia de esta versión llena la entrada
    // compartida sin los códigos que no conoce, la versión siguiente lee esa entrada sin su permiso nuevo y responde 403
    // a quien lo tiene (por ejemplo formcatalogs.read, que suma la E12) hasta que vence.
    [Fact]
    public async Task The_shared_permission_cache_keeps_a_code_that_only_the_next_version_knows()
    {
        using var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Próxima versión", Ct);
        // La migración de la versión siguiente le da al Dueño su permiso nuevo y avanza la generación de los roles.
        await using (var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, tenant, Ct))
        await using (var command = new NpgsqlCommand("""
            UPDATE tenant."Roles" SET "Permissions" = "Permissions" || ARRAY['formcatalogs.read']
            WHERE "IsSystemRole" AND "Name" = 'TenantAdmin'
            """, connection))
            Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
        await CacheGenerations.AdvanceAsync(factory.Services, Ct, CacheTags.Roles(tenant));

        // Una copia de esta versión, que sigue atendiendo, llena la entrada compartida.
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        var cached = await scope.ServiceProvider.GetRequiredService<IRolePermissionCache>()
            .GetOrganizationPermissionsAsync(tenant, owner.UserId, Ct);

        Assert.Contains("formcatalogs.read", cached);
    }

    private static HttpClient Client(ApiFactory target) =>
        target.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static string[] Codes(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];

    private static async Task MigrateAsync(ApiFactory target)
    {
        var operations = new CompleteIncludedPermissions().UpOperations.OfType<SqlOperation>().ToArray();
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

    // Un rol como los que guardaba la versión anterior: lo pedido, en orden, sin lo incluido.
    private static async Task<Guid> AddOldRoleAsync(ApiFactory target, Guid tenantId, string name, string[] permissions)
    {
        var role = Role.Create(name, null, RoleScope.Organization, null, permissions).Value;
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.ServiceProvider.GetRequiredService<IRoleRepository>().Add(role);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return role.Id;
    }

    private static async Task<string[]> UserEditsAsync(ApiFactory target, Guid tenantId, Guid roleId)
    {
        await using var services = target.Services.CreateAsyncScope();
        using var tenant = services.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await services.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>()
            .AsNoTracking()
            .Where(entry => entry.EntityId == roleId && entry.ActorKind == AuditActorKind.User
                && entry.Action == AuditAction.Updated)
            .Select(entry => entry.Changes)
            .ToArrayAsync(Ct);
    }
}
