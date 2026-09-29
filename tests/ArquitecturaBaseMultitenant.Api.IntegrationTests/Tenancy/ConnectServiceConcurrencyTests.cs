using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class ConnectServiceConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_first_consumer_access_creates_one_personal_space()
    {
        using var client = factory.CreateClient();
        Guid userId;
        var business = Tenant.CreateBusiness("Empresa A", requiresApproval: false);
        Assert.True(business.Activate().IsSuccess);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            userId = Guid.Empty;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                userId = (await services.GetRequiredService<IUserRepository>()
                    .CreateAsync("Ana", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                services.GetRequiredService<ITenantRepository>().Add(business);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(business.Id);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var member = Member.Invite(userId);
                Assert.True(member.Activate(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc)).IsSuccess);
                services.GetRequiredService<IMemberRepository>().Add(member);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        var first = SelectConsumerAsync(userId);
        var second = SelectConsumerAsync(userId);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess,
            result.IsFailure ? result.Error.Code : null));
        Assert.Equal(results[0].Value.TenantId, results[1].Value.TenantId);
        Assert.Equal(TenantKind.Personal, results[0].Value.TenantKind);

        await using var readScope = factory.Services.CreateAsyncScope();
        var accesses = await readScope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>()
            .ListForUserAsync(userId, Ct);
        Assert.Single(accesses, row => row.Kind == TenantKind.Personal);
        Assert.Single(accesses, row => row.Kind == TenantKind.Business && row.TenantId == business.Id);
    }

    private async Task<Result<ArquitecturaBaseMultitenant.Application.Models.Auth.ConnectUser>> SelectConsumerAsync(
        Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IConnectService>()
            .SelectAccessAsync(userId, Access.Consumer, null, Ct);
    }
}
