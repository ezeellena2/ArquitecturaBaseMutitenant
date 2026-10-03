using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformProvisioningTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invited_owner_is_provisioned_without_identity_and_replay_does_not_duplicate()
    {
        var tenant = Tenant.CreateBusiness("Dueño invitado", false);
        Assert.True(tenant.Activate().IsSuccess);
        var owner = Member.Invite();
        var company = Company.Create("Primera empresa", null, "UTC").Value;
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var services = setup.ServiceProvider;
            using var lease = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                services.GetRequiredService<ITenantRepository>().Add(tenant);
                services.GetRequiredService<IMemberRepository>().Add(owner);
                services.GetRequiredService<ICompanyRepository>().Add(company);
                return await services.GetRequiredService<TenantProvisioner>().StageBusinessOwnerAsync(tenant,
                    TenantSettings.Create("es-AR", "UTC", "ARS"), owner, ct, company.Id);
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(created.IsSuccess);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using var active = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        var repeated = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var persisted = await scope.ServiceProvider.GetRequiredService<ITenantRepository>().GetByIdAsync(tenant.Id, ct);
            var member = await scope.ServiceProvider.GetRequiredService<IMemberRepository>().GetByIdAsync(owner.Id, ct);
            return await scope.ServiceProvider.GetRequiredService<TenantProvisioner>().StageBusinessOwnerAsync(persisted!,
                TenantSettings.Create("es-AR", "UTC", "ARS"), member!, ct, company.Id);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(2, await context.Roles.CountAsync(Ct));
        Assert.Equal(2, await context.RoleAssignments.CountAsync(Ct));
        Assert.Equal(1, await context.CompanyMemberships.CountAsync(Ct));
        Assert.Equal(1, await context.TenantSettings.CountAsync(Ct));
        var stored = Assert.Single(await context.Members.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(MemberStatus.Invited, stored.Status);
        Assert.Null(stored.UserId);
        var admin = await context.Roles.SingleAsync(r => r.Name == SystemRoles.TenantAdmin, Ct);
        Assert.Contains(await context.RoleAssignments.AsNoTracking().ToListAsync(Ct), a => a.MemberId == owner.Id && a.RoleId == admin.Id && a.CompanyId == null);
    }

    [Fact]
    public async Task Different_preferences_are_rejected_without_replacing_the_existing_space()
    {
        var tenant = Tenant.CreateBusiness("Preferencias del alta", false);
        Assert.True(tenant.Activate().IsSuccess);
        var owner = Member.Invite();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var active = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            services.GetRequiredService<IMemberRepository>().Add(owner);
            return await services.GetRequiredService<TenantProvisioner>().StageBusinessOwnerAsync(tenant,
                TenantSettings.Create("es-AR", "UTC", "ARS"), owner, ct);
        }, CommitPolicy.OnSuccess, Ct);
        var conflict = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            services.GetRequiredService<TenantProvisioner>().StageBusinessOwnerAsync(tenant,
                TenantSettings.Create("en-US", "UTC", "USD"), owner, ct), CommitPolicy.OnSuccess, Ct);
        Assert.Equal(TenantErrors.ProvisioningConflictCode, conflict.Error.Code);
        Assert.Equal("es-AR", (await services.GetRequiredService<ApplicationDbContext>().TenantSettings.AsNoTracking().SingleAsync(Ct)).DefaultCulture);
    }
}
