using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class UserTenantAccessReaderTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reader_lists_only_account_memberships_across_tenant_scopes()
    {
        using var client = factory.CreateClient();
        Guid userId;
        Guid otherId;
        var alpha = Tenant.CreateBusiness("Alfa", requiresApproval: false);
        var beta = Tenant.CreateBusiness("Beta", requiresApproval: false);
        var personal = Tenant.CreatePersonal("Personal");
        Assert.True(alpha.Activate().IsSuccess);
        Assert.True(beta.Activate().IsSuccess);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            userId = Guid.Empty;
            otherId = Guid.Empty;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                otherId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                var tenants = services.GetRequiredService<ITenantRepository>();
                tenants.Add(alpha);
                tenants.Add(beta);
                tenants.Add(personal);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var older = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var newer = older.AddDays(1);
        await AddMemberAsync(alpha.Id, userId, older);
        await AddMemberAsync(beta.Id, userId, newer);
        await AddMemberAsync(beta.Id, otherId, newer);
        await AddMemberAsync(personal.Id, userId, newer);

        await using var readScope = factory.Services.CreateAsyncScope();
        var accesses = await readScope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>()
            .ListForUserAsync(userId, Ct);

        Assert.Equal(3, accesses.Count);
        Assert.Contains(accesses, row => row.TenantId == alpha.Id && row.Kind == TenantKind.Business &&
            row.Name == "Alfa" && row.JoinedAtUtc == older && row.MemberStatus == MemberStatus.Active);
        Assert.Contains(accesses, row => row.TenantId == beta.Id && row.Kind == TenantKind.Business &&
            row.Name == "Beta" && row.JoinedAtUtc == newer);
        Assert.Contains(accesses, row => row.TenantId == personal.Id && row.Kind == TenantKind.Personal);
    }

    private async Task AddMemberAsync(Guid tenantId, Guid userId, DateTime joinedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var active = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var member = Member.Invite(userId);
            Assert.True(member.Activate(joinedAtUtc).IsSuccess);
            services.GetRequiredService<IMemberRepository>().Add(member);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
