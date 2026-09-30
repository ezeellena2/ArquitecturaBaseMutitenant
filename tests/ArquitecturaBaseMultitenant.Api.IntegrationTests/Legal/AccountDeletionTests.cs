using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Module_block_preserves_the_account_and_does_not_consume_ownership_ticket()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var secrets = services.GetRequiredService<ISecureTokenGenerator>();
        var secret = secrets.Generate();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var source = await context.LoginMethods.SingleAsync(row => row.UserId == account.UserId && row.IsPrimary, Ct);
        var ticket = ReauthTicket.Issue(account.UserId, ReauthAction.DeleteAccount, source.Id, null, secrets.Hash(secret), nowUtc).Value;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            context.ReauthTickets.Add(ticket);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var participants = services.GetServices<IAccountDeletionParticipant>().Append(new BlockingModule());
        var requester = ActivatorUtilities.CreateInstance<AccountDeletionRequester>(services, participants);
        var service = ActivatorUtilities.CreateInstance<AccountDeletionService>(services, new TestUser(account.UserId), requester);
        var result = await service.RequestAsync(new RequestAccountDeletionRequest("Ya no la uso", secret), Ct);

        Assert.Equal("Legal.AccountDeletion.Blocked", result.Error.Code);
        Assert.Equal(UserStatus.Active, (await services.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!.Status);
        Assert.Null((await context.ReauthTickets.AsNoTracking().SingleAsync(row => row.Id == ticket.Id, Ct)).ConsumedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletion_requires_recent_ownership_and_operators_cannot_request_it(bool platformOperator)
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        if (platformOperator)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await manager.FindByIdAsync(account.UserId.ToString("D"));
                user!.GrantPlatformOperator();
                Assert.True((await manager.UpdateAsync(user)).Succeeded);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var rejected = await AccountJourney.PostAsync(client, "/api/me/deletion", new { reason = "Ya no la uso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(platformOperator ? "Legal.AccountDeletion.PlatformOperator" : "Legal.AccountDeletion.ReauthRequired",
            problem!.RootElement.GetProperty("code").GetString());
        await using var checkedScope = factory.Services.CreateAsyncScope();
        Assert.Equal(UserStatus.Active, (await checkedScope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Fact]
    public async Task Request_commits_grace_and_revokes_consumer_business_and_cookie_sessions()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var consumerToken = client.DefaultRequestHeaders.Authorization!.Parameter;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var business = Tenant.CreateBusiness("Empresa baja HTTP", false);
        business.Activate();
        using (services.GetRequiredService<ITenantScope>().Enter(business.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var businessToken = client.DefaultRequestHeaders.Authorization!.Parameter;
        var secrets = services.GetRequiredService<ISecureTokenGenerator>();
        var secret = secrets.Generate();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var primary = await context.LoginMethods.AsNoTracking().SingleAsync(row => row.UserId == account.UserId && row.IsPrimary, Ct);
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.ReauthTickets.Add(ReauthTicket.Issue(account.UserId, ReauthAction.DeleteAccount,
                primary.Id, null, secrets.Hash(secret), nowUtc).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        using var requested = await AccountJourney.PostAsync(client, "/api/me/deletion",
            new { reason = "Ya no la uso", reauthTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        using var result = await requested.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.True(result!.RootElement.GetProperty("scheduledForUtc").GetDateTime() >= nowUtc.AddDays(30));
        foreach (var token in new[] { consumerToken, businessToken })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var denied = await client.GetAsync("/api/me", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        Assert.Contains(requested.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        context.ChangeTracker.Clear();
        var row = await services.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct);
        Assert.Equal(UserStatus.PendingDeletion, row!.Status);
        Assert.NotNull(row.DeletionScheduledForUtc);
        Assert.Single(await context.LoginMethods.AsNoTracking().Where(method => method.UserId == account.UserId).ToArrayAsync(Ct));
        Assert.False(await context.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().AnyAsync(
            token => token.Subject == account.UserId.ToString("D") && token.Status == "valid", Ct));
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private sealed class TestUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
        public Access? Access => Domain.Users.Access.Consumer;
    }
    private sealed class BlockingModule : IAccountDeletionParticipant
    {
        public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Error>>([Domain.Legal.AccountDeletionErrors.Blocked]);
        public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => throw new InvalidOperationException();
        public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
        public Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
