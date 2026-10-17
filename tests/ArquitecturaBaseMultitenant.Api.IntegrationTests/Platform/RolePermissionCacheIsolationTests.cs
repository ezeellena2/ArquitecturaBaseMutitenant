using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>
/// La caché compartida de permisos (ADR 0052) ignora entradas viejas, cambia con la generación de los roles y no cruza
/// organizaciones: cada clave privada se lee sólo desde un pedido de su organización, bajo RLS.
/// </summary>
public sealed class RolePermissionCacheIsolationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    [Fact]
    public async Task Legacy_entries_are_ignored_role_changes_advance_the_cache_and_other_tenants_are_isolated()
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
        // Entradas de antes de E9: sin versión de esquema ni generaciones en la clave, nunca se leen.
        var legacyKeys = new[]
        {
            CacheKeys.Tenant(first.Id, "role-permissions:" + roleId.ToString("N")),
            CacheKeys.Tenant(first.Id, "role-permissions:v2:" + roleId.ToString("N")),
            CacheKeys.Tenant(first.Id, "permissions:" + owner.UserId.ToString("N") + ":organization"),
        };
        foreach (var key in legacyKeys)
            await cache.SetAsync(key, new[] { Permissions.Roles.Read }, cancellationToken: Ct);

        Assert.Equal(Permissions.All.Order(StringComparer.Ordinal), await OrganizationAsync(factory, first.Id, owner.UserId));
        Assert.Equal(Permissions.All.Order(StringComparer.Ordinal), await OrganizationAsync(factory, other.Id, owner.UserId));
        await SqlAsync(factory, first.Id, """
            UPDATE tenant."Roles" SET "Permissions" = array_remove("Permissions", 'roles.manage')
            WHERE "Name" = 'TenantAdmin';
            """);
        // El SQL no pasó por el guardado: la entrada sigue vigente hasta que avanza la generación de los roles.
        Assert.Contains(Permissions.Roles.Manage, await OrganizationAsync(factory, first.Id, owner.UserId));
        await CacheGenerations.AdvanceAsync(factory.Services, Ct, CacheTags.Roles(first.Id));
        Assert.DoesNotContain(Permissions.Roles.Manage, await OrganizationAsync(factory, first.Id, owner.UserId));
        Assert.Contains(Permissions.Roles.Manage, await OrganizationAsync(factory, other.Id, owner.UserId));
        foreach (var key in legacyKeys)
            Assert.Equal(new[] { Permissions.Roles.Read }, await cache.GetOrCreateAsync(key,
                _ => ValueTask.FromResult(Array.Empty<string>()), cancellationToken: Ct));

        // Una clave privada de otra organización no se usa desde este pedido.
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(first.Id, TenantKind.Business);
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<IRolePermissionCache>().GetOrganizationPermissionsAsync(other.Id, owner.UserId, Ct));
    }

    private static async Task<IReadOnlyList<string>> OrganizationAsync(ApiFactory factory, Guid tenantId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        return await scope.ServiceProvider.GetRequiredService<IRolePermissionCache>()
            .GetOrganizationPermissionsAsync(tenantId, userId, Ct);
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

    private static async Task SqlAsync(ApiFactory factory, Guid tenantId, string sql)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, tenantId, Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<RoleRow[]> RolesAsync(ApiFactory factory, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var roles = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.AsNoTracking()
            .OrderBy(r => r.Name).ToArrayAsync(Ct);
        return roles.Select(r => new RoleRow(r.Id, r.Name, r.Scope, r.CompanyId, r.IsSystemRole, [.. r.Permissions])).ToArray();
    }

    private sealed record RoleRow(Guid Id, string Name, RoleScope Scope, Guid? CompanyId, bool IsSystemRole, string[] Permissions);
}
