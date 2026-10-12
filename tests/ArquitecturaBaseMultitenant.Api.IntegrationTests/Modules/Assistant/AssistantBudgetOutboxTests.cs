using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantBudgetOutboxTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);

    [Fact]
    public async Task A_pending_own_key_alert_is_enqueued_once_for_the_real_verified_owner_across_two_hosts()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var organizationId = await OrganizationJourney.StageAsync(factory, account, "Budget owner", Ct);
        var alert = AssistantBudgetAlert.Create(organizationId, Guid.NewGuid(), AssistantKeyFunding.OwnKey, 80, Usd(80), Usd(100));
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(alert);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct)).IsSuccess);
        }
        long before;
        using (var scope = factory.Services.CreateScope()) before = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .OutboxMessages.CountAsync(row => row.UserId == account.UserId, Ct);
        using var replica = factory.WithWebHostBuilder(_ => { });
        _ = replica.Services;
        var results = await Task.WhenAll(RetryAsync(factory.Services, alert.Id), RetryAsync(replica.Services, alert.Id));
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error.Code));
        using var read = factory.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull((await db.Set<AssistantBudgetAlert>().AsNoTracking().SingleAsync(row => row.Id == alert.Id, Ct)).EnqueuedAtUtc);
        Assert.Equal(before + 1, await db.OutboxMessages.CountAsync(row => row.UserId == account.UserId, Ct));
        Assert.True((await RetryAsync(factory.Services, alert.Id)).IsSuccess);
        Assert.Equal(before + 1, await db.OutboxMessages.CountAsync(row => row.UserId == account.UserId, Ct));
    }

    [Fact]
    public async Task An_alert_without_a_verified_owner_stays_pending()
    {
        var alert = AssistantBudgetAlert.Create(Guid.NewGuid(), Guid.NewGuid(), AssistantKeyFunding.OwnKey, 100, Usd(100), Usd(100));
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(alert);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct)).IsSuccess);
        }
        Assert.True((await RetryAsync(factory.Services, alert.Id)).IsSuccess);
        using var read = factory.Services.CreateScope();
        Assert.Null((await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantBudgetAlert>()
            .AsNoTracking().SingleAsync(row => row.Id == alert.Id, Ct)).EnqueuedAtUtc);
    }
    private static async Task<Result> RetryAsync(IServiceProvider provider, Guid id)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAssistantBudgetAlertService>().RetryAsync(id, Ct);
    }
}
