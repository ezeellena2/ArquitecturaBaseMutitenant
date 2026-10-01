using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el primer acceso personal simultáneo cree un único espacio para la cuenta. Protege el alta
/// frente a dos solicitudes que llegan juntas.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ConnectServiceConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_first_consumer_access_creates_one_personal_space()
    {
        var gate = new PrepareGate();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var descriptor = Assert.Single(services, item =>
                item.ServiceType == typeof(IPersonalSpaceProvisioner));
            services.Remove(descriptor);
            services.AddScoped<IPersonalSpaceProvisioner>(provider => new GatedPersonalSpaceProvisioner(
                (IPersonalSpaceProvisioner)ActivatorUtilities.CreateInstance(
                    provider, descriptor.ImplementationType!), gate));
        }));
        using var client = host.CreateClient();
        Guid userId;
        var business = Tenant.CreateBusiness("Empresa A", requiresApproval: false);
        Assert.True(business.Activate().IsSuccess);
        await using (var scope = host.Services.CreateAsyncScope())
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

        await using (var scope = host.Services.CreateAsyncScope())
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

        var first = SelectConsumerAsync(host.Services, userId);
        var second = SelectConsumerAsync(host.Services, userId);
        var results = await Task.WhenAll(first, second);

        Assert.True(gate.BothPrepared.Task.IsCompletedSuccessfully);
        Assert.All(results, result => Assert.True(result.IsSuccess,
            result.IsFailure ? result.Error.Code : null));
        Assert.Equal(results[0].Value.TenantId, results[1].Value.TenantId);
        Assert.Equal(TenantKind.Personal, results[0].Value.TenantKind);

        await using var readScope = host.Services.CreateAsyncScope();
        var accesses = await readScope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>()
            .ListForUserAsync(userId, Ct);
        Assert.Single(accesses, row => row.Kind == TenantKind.Personal);
        Assert.Single(accesses, row => row.Kind == TenantKind.Business && row.TenantId == business.Id);
    }

    private static async Task<Result<ArquitecturaBaseMultitenant.Application.Models.Auth.ConnectUser>> SelectConsumerAsync(
        IServiceProvider provider, Guid userId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IConnectService>()
            .SelectAccessAsync(userId, Access.Consumer, null, Ct);
    }

    private sealed class PrepareGate
    {
        private int _arrivals;

        public TaskCompletionSource BothPrepared { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) BothPrepared.TrySetResult();
            await BothPrepared.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private sealed class GatedPersonalSpaceProvisioner(IPersonalSpaceProvisioner inner, PrepareGate gate)
        : IPersonalSpaceProvisioner
    {
        public async Task<PersonalSpaceDraft> PrepareAsync(string? cultureCode, string? browserTimeZoneId,
            CancellationToken cancellationToken)
        {
            var draft = await inner.PrepareAsync(cultureCode, browserTimeZoneId, cancellationToken);
            await gate.ArriveAsync(cancellationToken);
            return draft;
        }

        public void Stage(PersonalSpaceDraft draft, Guid userId) => inner.Stage(draft, userId);
    }
}
