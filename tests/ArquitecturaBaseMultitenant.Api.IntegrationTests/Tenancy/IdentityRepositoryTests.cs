using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class IdentityRepositoryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Repositories_require_the_use_case_transaction_for_writes()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<ITenantRepository>().Add(Tenant.CreatePersonal("Personal")));
        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<IMemberRepository>().Add(Member.Invite(Guid.CreateVersion7())));
        Assert.Throws<InvalidOperationException>(() =>
            services.GetRequiredService<ITenantSettingsRepository>().Add(
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS")));
    }

    [Fact]
    public async Task Member_reader_starts_at_members_and_never_crosses_active_tenant()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();
        var tenants = services.GetRequiredService<ITenantRepository>();
        var members = services.GetRequiredService<IMemberRepository>();
        var memberReader = services.GetRequiredService<IMemberReader>();
        var tenantReader = services.GetRequiredService<ITenantReader>();
        var userA = ApplicationUser.Create("Ana", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var userB = ApplicationUser.Create("Beto", "es-AR", "America/Argentina/Buenos_Aires").Value;
        var businessA = Tenant.CreateBusiness("Empresa A", requiresApproval: false);
        var businessB = Tenant.CreateBusiness("Empresa B", requiresApproval: false);
        var joinedAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        Assert.True(businessA.Activate().IsSuccess);
        Assert.True(businessB.Activate().IsSuccess);

        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            context.Users.AddRange(userA, userB);
            tenants.Add(businessA);
            tenants.Add(businessB);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        using (tenantScope.Enter(businessA.Id))
        {
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                var member = Member.Invite(userA.Id);
                Assert.True(member.Activate(joinedAtUtc).IsSuccess);
                members.Add(member);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);

            var listed = await memberReader.ListCurrentTenantAsync(Ct);
            var only = Assert.Single(listed);
            Assert.Equal(userA.Id, only.UserId);
            Assert.Equal("Ana", only.DisplayName);
            Assert.Null(await memberReader.FindByUserIdAsync(userB.Id, Ct));
        }

        using (tenantScope.Enter(businessB.Id))
        {
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                var member = Member.Invite(userB.Id);
                Assert.True(member.Activate(joinedAtUtc).IsSuccess);
                members.Add(member);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);

            Assert.Equal(userB.Id, Assert.Single(await memberReader.ListCurrentTenantAsync(Ct)).UserId);
            Assert.Null(await memberReader.FindByUserIdAsync(userA.Id, Ct));
        }

        var active = await tenantReader.ListActiveBusinessesAsync(Ct);
        Assert.Contains(active, row => row.Id == businessA.Id);
        Assert.Contains(active, row => row.Id == businessB.Id);
    }

    [Fact]
    public async Task Tenant_settings_reader_keys_cache_by_active_tenant()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();
        var tenants = services.GetRequiredService<ITenantRepository>();
        var settingsRepository = services.GetRequiredService<ITenantSettingsRepository>();
        var settingsReader = services.GetRequiredService<ITenantSettingsReader>();
        var personalA = Tenant.CreatePersonal("Personal A");
        var personalB = Tenant.CreatePersonal("Personal B");

        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            tenants.Add(personalA);
            tenants.Add(personalB);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        using (tenantScope.Enter(personalA.Id))
        {
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                settingsRepository.Add(TenantSettings.Create(
                    "es-AR", "America/Argentina/Buenos_Aires", "ARS"));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);

            Assert.Equal("es-AR", (await settingsReader.FindCurrentAsync(Ct))?.DefaultCulture);
        }

        using (tenantScope.Enter(personalB.Id))
        {
            Assert.Null(await settingsReader.FindCurrentAsync(Ct));
        }

        using (tenantScope.Enter(personalA.Id))
        {
            Assert.Equal("es-AR", (await settingsReader.FindCurrentAsync(Ct))?.DefaultCulture);
        }
    }
}
