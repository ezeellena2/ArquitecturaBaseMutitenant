using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantNoticeAuthorizationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_exclusive_operator_accepts_explicitly_with_no_personal_profile_or_tenant_scope()
    {
        await factory.Services.GetRequiredService<ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed.DatabaseSeeder>()
            .SeedAsync(Ct);
        Guid operatorId;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                Result.Success(await services.GetRequiredService<IUserRepository>().CreateAsync("Exclusive notice operator", "es-AR",
                    "America/Argentina/Buenos_Aires", ct)), CommitPolicy.OnSuccess, Ct);
            operatorId = created.Value.Id;
        }
        await PlatformOperators.GrantAsync(factory.Services, operatorId, [PlatformPermissions.Assistant.Use], Ct);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new OperatorActor(operatorId)))));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        Assert.Null(provider.GetRequiredService<ITenantContext>().TenantId);
        var legal = provider.GetRequiredService<ILegalAcceptanceService>();
        var pending = await legal.GetPendingAsync(Ct);
        Assert.Equal(2, pending.Value.Count);
        var notice = provider.GetRequiredService<IAssistantConversationService>();
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Legal.LegalErrors.AcceptanceRequiredCode,
            (await notice.AcceptNoticeAsync(new AssistantNoticeRequest("1"), Ct)).Error.Code);
        Assert.True((await legal.AcceptAsync(new AcceptLegalRequest(pending.Value.Select(row => new LegalAcceptanceItem(row.Id, row.Version)).ToArray()), Ct)).IsSuccess);
        Assert.True((await notice.AcceptNoticeAsync(new AssistantNoticeRequest("1"), Ct)).IsSuccess);
        Assert.Null(provider.GetRequiredService<ITenantContext>().TenantId);
        var row = await provider.GetRequiredService<ApplicationDbContext>().Set<AssistantNoticeAcceptance>().AsNoTracking()
            .SingleAsync(value => value.OwnerUserId == operatorId, Ct);
        Assert.Equal(Access.Platform, row.Access);
    }

    private sealed record OperatorActor(Guid Id) : ICurrentUser
    {
        public Guid? UserId => Id;
        public Access? Access => Domain.Users.Access.Platform;
    }

    [Theory]
    [InlineData("write", "suspend", HttpStatusCode.Forbidden)]
    [InlineData("replay", "suspend", HttpStatusCode.Forbidden)]
    [InlineData("replay", "permission", HttpStatusCode.Forbidden)]
    [InlineData("replay", "gate", HttpStatusCode.NotFound)]
    public async Task A_revocation_after_middleware_is_rechecked_before_write_or_replay(string phase, string revoke, HttpStatusCode expected)
    {
        var pause = new Pause(phase);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var repository = services.Single(row => row.ServiceType == typeof(IAssistantNoticeRepository));
            services.Replace(ServiceDescriptor.Scoped<IAssistantNoticeRepository>(provider => new PausedNoticeRepository(
                (IAssistantNoticeRepository)ActivatorUtilities.CreateInstance(provider, repository.ImplementationType!), pause)));
            var store = services.Single(row => row.ServiceType == typeof(IIdempotencyStore));
            services.Replace(ServiceDescriptor.Singleton<IIdempotencyStore>(provider => new PausedStore(
                (IIdempotencyStore)(store.ImplementationInstance ?? store.ImplementationFactory?.Invoke(provider)
                    ?? ActivatorUtilities.CreateInstance(provider, store.ImplementationType!)), pause)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var route = "/api/me/assistant/notice";
        if (revoke == "permission")
        {
            await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Assistant.Use], Ct);
            await AccountJourney.AuthorizeAsync(client, "platform", Ct);
            route = "/api/platform/assistant/notice";
        }
        var key = Guid.NewGuid();
        if (phase == "replay")
        {
            using var initial = await AssistantNoticeTests.SendAsync(client, "1", key, route);
            Assert.Equal(HttpStatusCode.NoContent, initial.StatusCode);
        }
        pause.Armed = true;
        var attempt = AssistantNoticeTests.SendAsync(client, "1", key, route);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        try
        {
            if (revoke == "gate") await AssistantTestData.PersonalAsync(factory, false);
            else if (revoke == "permission") await PlatformOperators.GrantAsync(host.Services, account.UserId, [PlatformPermissions.Audit.Read], Ct);
            else
            {
                using var scope = host.Services.CreateScope();
                var services = scope.ServiceProvider;
                await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    Assert.True((await services.GetRequiredService<IUserRepository>().SuspendAsync(account.UserId, ct)).IsSuccess);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, Ct);
                await services.GetRequiredService<IAccessStatusCache>().InvalidateUserAsync(account.UserId, Ct);
            }
        }
        finally { pause.Resume.TrySetResult(); }
        using var rejected = await attempt;
        Assert.Equal(expected, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Idempotent-Replayed"));
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.True(problem!.RootElement.GetProperty("traceId").GetString()?.Length > 0);
        Assert.Equal((int)expected, problem.RootElement.GetProperty("status").GetInt32());
        using var verify = factory.Services.CreateScope();
        Assert.Equal(phase == "replay" ? 1 : 0, await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<AssistantNoticeAcceptance>().CountAsync(row => row.OwnerUserId == account.UserId, Ct));
    }

    private sealed class Pause(string phase)
    {
        internal bool Armed { get; set; }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async Task AtAsync(string current, CancellationToken ct)
        {
            if (!Armed || phase != current) return;
            Armed = false;
            Started.TrySetResult();
            await Resume.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
    }

    private sealed record PausedNoticeRepository(IAssistantNoticeRepository Inner, Pause Pause) : IAssistantNoticeRepository
    {
        public async Task LockAsync(Guid ownerUserId, CancellationToken ct) { await Pause.AtAsync("write", ct); await Inner.LockAsync(ownerUserId, ct); }
        public void Add(AssistantNoticeAcceptance acceptance) => Inner.Add(acceptance);
    }

    private sealed record PausedStore(IIdempotencyStore Inner, Pause Pause) : IIdempotencyStore
    {
        public async Task<IdempotencyReservation> ReserveAsync(Guid? tenantId, Guid userId, Guid key, string bodyHash, string route, CancellationToken cancellationToken)
        {
            var result = await Inner.ReserveAsync(tenantId, userId, key, bodyHash, route, cancellationToken);
            if (result.State == IdempotencyReservationState.Replay) await Pause.AtAsync("replay", cancellationToken);
            return result;
        }
        public Task CompleteAsync(Guid id, int statusCode, string responseBody, CancellationToken cancellationToken) => Inner.CompleteAsync(id, statusCode, responseBody, cancellationToken);
        public Task ReleaseAsync(Guid id, CancellationToken cancellationToken) => Inner.ReleaseAsync(id, cancellationToken);
        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken) => Inner.CleanupExpiredAsync(cancellationToken);
    }
}
