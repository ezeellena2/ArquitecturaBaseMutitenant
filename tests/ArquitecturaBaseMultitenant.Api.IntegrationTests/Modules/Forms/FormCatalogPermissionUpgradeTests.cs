using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>El Dueño de cada organización existente recibe los permisos de los catálogos una sola vez, y nadie más (E12, K1).</summary>
public sealed class FormCatalogPermissionUpgradeTests
{
    private const string Upgrade = "20261127120737_SystemRoleFormCatalogPermissions";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Added = [Permissions.Forms.CatalogsRead, Permissions.Forms.CatalogsManage];

    [Fact]
    public async Task Existing_owners_receive_the_missing_catalog_permissions_once_and_other_roles_do_not()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Catálogos upgrade", Ct);
        await SqlAsync(factory, tenant, """
            UPDATE tenant."Roles" SET "Permissions" = ARRAY(SELECT permission FROM unnest("Permissions") AS permission WHERE permission NOT LIKE 'formcatalogs.%')
            WHERE "IsSystemRole";
            """);
        // Una instalación que ya tenía uno de los permisos no lo duplica.
        await SqlAsync(factory, tenant, """UPDATE tenant."Roles" SET "Permissions" = array_append("Permissions", 'formcatalogs.read') WHERE "Name" = 'TenantAdmin';""");
        var before = await RolesAsync(factory, tenant);

        await using var db = AdminContext(factory);
        var migrator = db.GetService<IMigrator>();
        var previous = Previous(db);
        await migrator.MigrateAsync(previous, Ct);
        await migrator.MigrateAsync(Upgrade, Ct);
        await migrator.MigrateAsync(previous, Ct);
        await migrator.MigrateAsync(Upgrade, Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);

        var after = await RolesAsync(factory, tenant);
        Assert.Equal(before.Length, after.Length);
        foreach (var role in after)
        {
            var old = before.Single(value => value.Id == role.Id);
            var expected = role.Name == SystemRoles.TenantAdmin && role.IsSystemRole
                ? old.Permissions.Concat(Added.Where(permission => !old.Permissions.Contains(permission)))
                : old.Permissions;
            Assert.Equal(expected, role.Permissions);
        }
        Assert.Equal(1, after.Single(role => role.Name == SystemRoles.TenantAdmin).Permissions.Count(permission => permission == Permissions.Forms.CatalogsRead));
        Assert.DoesNotContain(after.Single(role => role.Name == SystemRoles.CompanyAdmin).Permissions,
            permission => permission.StartsWith("formcatalogs.", StringComparison.Ordinal));
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
        var roles = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.AsNoTracking().OrderBy(role => role.Name).ToArrayAsync(Ct);
        return roles.Select(role => new RoleRow(role.Id, role.Name, role.IsSystemRole, [.. role.Permissions])).ToArray();
    }

    private sealed record RoleRow(Guid Id, string Name, bool IsSystemRole, string[] Permissions);
}
