using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>Las organizaciones y los operadores existentes reciben los permisos de WhatsApp una sola vez (D3 y lección A1/M1).</summary>
public sealed class WhatsAppPermissionUpgradeTests
{
    private const string Upgrade = "20261013222720_WhatsAppAttentionPermissions";
    private const string Reason = "Platform operator WhatsApp permissions upgrade";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] OrganizationAdded =
        [Permissions.WhatsApp.Customers, Permissions.WhatsApp.Internal, Permissions.WhatsApp.Broadcasts, Permissions.WhatsApp.Manage];

    [Fact]
    public async Task Existing_owners_and_operator_managers_receive_the_missing_permissions_once()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "WhatsApp upgrade", Ct);
        await SqlAsync(factory, tenant, """
            UPDATE tenant."Roles" SET "Permissions" = ARRAY(SELECT permission FROM unnest("Permissions") AS permission WHERE permission NOT LIKE 'whatsapp.%')
            WHERE "IsSystemRole";
            """);
        // Una instalación que ya tenía uno de los permisos no lo duplica.
        await SqlAsync(factory, tenant, """UPDATE tenant."Roles" SET "Permissions" = array_append("Permissions", 'whatsapp.manage') WHERE "Name" = 'TenantAdmin';""");
        var before = await RolesAsync(factory, tenant);
        using var managerClient = Client(factory);
        var manager = await AccountJourney.RegisterAsync(factory, managerClient, Ct);
        using var readerClient = Client(factory);
        var reader = await AccountJourney.RegisterAsync(factory, readerClient, Ct);
        await PlatformOperators.GrantAsync(factory.Services, manager.UserId, [PlatformPermissions.Operators.Manage, PlatformPermissions.WhatsApp.Attend], Ct);
        await PlatformOperators.GrantAsync(factory.Services, reader.UserId, [PlatformPermissions.Accounts.Read], Ct);

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
                ? old.Permissions.Concat(OrganizationAdded.Where(permission => !old.Permissions.Contains(permission)))
                : old.Permissions;
            Assert.Equal(expected, role.Permissions);
        }
        Assert.DoesNotContain(after.Single(role => role.Name == SystemRoles.CompanyAdmin).Permissions, permission => permission.StartsWith("whatsapp.", StringComparison.Ordinal));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assignments = await context.PlatformPermissionAssignments.AsNoTracking()
            .Where(value => value.UserId == manager.UserId || value.UserId == reader.UserId).ToDictionaryAsync(value => value.UserId, Ct);
        Assert.Equal([PlatformPermissions.Operators.Manage, PlatformPermissions.WhatsApp.Attend, PlatformPermissions.WhatsApp.Manage],
            assignments[manager.UserId].Permissions);
        Assert.Equal([PlatformPermissions.Accounts.Read], assignments[reader.UserId].Permissions);
        var evidence = await context.SecurityEvents.AsNoTracking().Where(value => value.Reason == Reason).ToArrayAsync(Ct);
        Assert.Equal(manager.UserId, Assert.Single(evidence).TargetAccountId);
    }

    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

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
