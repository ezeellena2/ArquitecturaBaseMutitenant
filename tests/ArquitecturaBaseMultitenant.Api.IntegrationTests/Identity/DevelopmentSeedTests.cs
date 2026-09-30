using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class DevelopmentSeedTests
{
    [Fact]
    public async Task Development_seed_creates_business_and_two_personal_spaces_once()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.3")
            .WithDatabase("postgres")
            .Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var ownerPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var runtimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var bootstrap = postgres.GetConnectionString();
        var admin = Connection("mt_owner", ownerPassword);
        var runtime = Connection("mt_app", runtimePassword);

        using var host = NewHost("ana@example.test");
        using var client = host.CreateClient();
        await host.Services.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenants = await context.Tenants.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, tenants.Length);
        var business = Assert.Single(tenants, tenant => tenant.Kind == TenantKind.Business);
        var personal = tenants.Where(tenant => tenant.Kind == TenantKind.Personal).ToArray();
        Assert.Equal(2, personal.Length);
        Assert.Equal("Empresa A", business.Name);
        Assert.Equal(TenantKind.Business, business.Kind);
        Assert.Equal(TenantStatus.Active, business.Status);
        var users = await context.Users.OrderBy(user => user.DisplayName)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["Ana", "Carla", "Kevin"], users.Select(user => user.DisplayName));
        Assert.Contains(users, user => user.DisplayName == "Ana" && user.Email == "ana@example.test");
        Assert.All(users.Where(user => user.Email != "ana@example.test"), user =>
            Assert.Matches(@"(?i)\.(test|example)$", user.Email!));
        Assert.Equal(3, await context.LoginMethods.CountAsync(TestContext.Current.CancellationToken));

        var personalOwners = new List<Guid>();
        foreach (var tenant in personal)
        {
            using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id))
            {
                var member = Assert.Single(await context.Members.AsNoTracking()
                    .ToArrayAsync(TestContext.Current.CancellationToken));
                Assert.Equal(MemberStatus.Active, member.Status);
                personalOwners.Add(member.UserId);
                Assert.Single(await context.TenantSettings.AsNoTracking()
                    .ToArrayAsync(TestContext.Current.CancellationToken));
            }
        }
        Assert.Equal(users.Where(user => user.DisplayName is "Carla" or "Kevin").Select(user => user.Id)
            .Order(), personalOwners.Order());

        // Otra organización puede usar el mismo nombre público; no es el identificador del seed.
        TenantSettings businessSettings;
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(business.Id))
        {
            businessSettings = Assert.Single(await context.TenantSettings.AsNoTracking()
                .ToArrayAsync(TestContext.Current.CancellationToken));
        }
        var homonym = Tenant.CreateBusiness("Empresa A", requiresApproval: false);
        Assert.True(homonym.Activate().IsSuccess);
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(homonym.Id))
        {
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                scope.ServiceProvider.GetRequiredService<TenantSpaceProvisioner>().Stage(homonym,
                    TenantSettings.Create(businessSettings.DefaultCulture, businessSettings.DefaultTimeZoneId,
                        businessSettings.DefaultCurrency), [users.Single(user => user.DisplayName == "Carla").Id]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);
        }

        using var changedConfigurationHost = NewHost("another@example.test");
        using var changedConfigurationClient = changedConfigurationHost.CreateClient();
        await changedConfigurationHost.Services.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        Assert.False(await context.LoginMethods.AnyAsync(method => method.Value == "another@example.test",
            TestContext.Current.CancellationToken));
        Assert.Equal(3, await context.LoginMethods.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, await context.Tenants.CountAsync(TestContext.Current.CancellationToken));

        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(business.Id))
        {
            var members = await context.Members.ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, members.Length);
            Assert.All(members, member => Assert.Equal(MemberStatus.Active, member.Status));
            Assert.Single(await context.TenantSettings.ToArrayAsync(TestContext.Current.CancellationToken));
        }

        // Un segundo proceso puede haber preparado el borrador antes de confirmar el primer seed.
        var staleDraft = await scope.ServiceProvider.GetRequiredService<IPersonalSpaceProvisioner>()
            .PrepareAsync(null, null, TestContext.Current.CancellationToken);
        var staleCandidate = new DevelopmentPersonalCandidate(staleDraft, "Kevin",
            Email.Create("kevin@empresa-a.test").Value);
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(staleDraft.Tenant.Id))
        {
            await Assert.ThrowsAsync<DevelopmentSeedScopeChangedException>(async () =>
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>()
                        .StagePersonalAsync(staleCandidate, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken));
        }

        WebApplicationFactory<Program> NewHost(string anaEmail) =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:postgres-bootstrap", bootstrap);
                builder.UseSetting("ConnectionStrings:appdb-admin", admin);
                builder.UseSetting("ConnectionStrings:appdb", runtime);
                builder.UseSetting("Authentication:Issuer", "https://example.test/");
                builder.UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://example.test/auth/callback");
                builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://example.test/");
                builder.UseSetting("Email:Delivery", "PickupDirectory");
                builder.UseSetting("Seed:Development:AnaEmail", anaEmail);
            });

        string Connection(string user, string password) => new NpgsqlConnectionStringBuilder(bootstrap)
        {
            Database = "appdb",
            Username = user,
            Password = password,
        }.ConnectionString;
    }
}
