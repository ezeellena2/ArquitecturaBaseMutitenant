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
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authorization;
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

/// <summary>
/// Comprueba permisos e impedimentos al solicitar una baja con sesión iniciada. Un bloqueo preserva la cuenta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Module_block_preserves_the_account_without_a_confirmation_code()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var participants = services.GetServices<IAccountDeletionParticipant>().Append(new BlockingModule());
        var scheduler = ActivatorUtilities.CreateInstance<AccountDeletionScheduler>(services, participants);
        var requester = ActivatorUtilities.CreateInstance<AccountDeletionRequester>(services, scheduler);
        var service = ActivatorUtilities.CreateInstance<AccountDeletionService>(services, new TestUser(account.UserId), requester);
        var result = await service.RequestAsync(new RequestAccountDeletionRequest("Ya no la uso"), Ct);

        Assert.Equal("Legal.AccountDeletion.Blocked", result.Error.Code);
        Assert.Equal(UserStatus.Active, (await services.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Fact]
    public async Task Sole_business_owner_cannot_request_deletion()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var business = Tenant.CreateBusiness("Organización con única Dueña", false);
        business.Activate();
        using (services.GetRequiredService<ITenantScope>().Enter(business.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        // Tablero Cuenta, «Es la única Dueña»: Mi cuenta sabe de antemano por qué no puede darse de baja.
        Assert.Equal(["Organización con única Dueña"], await SoleOwnerOrganizationsAsync(client));

        using var response = await AccountJourney.PostAsync(client, "/api/me/deletion",
            new { reason = "Ya no la uso" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Legal.AccountDeletion.LastAdmin", problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(UserStatus.Active, (await services.GetRequiredService<IUserRepository>()
            .GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Fact]
    public async Task The_sole_owner_of_a_suspended_organization_cannot_request_deletion_either()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var business = Tenant.CreateBusiness("Organización suspendida con única Dueña", false);
        Assert.True(business.Activate().IsSuccess);
        Assert.True(business.Suspend().IsSuccess);
        using (services.GetRequiredService<ITenantScope>().Enter(business.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);

        // ADR 0035 (D5): bloquean todas las organizaciones no cerradas, como en la baja que inicia Plataforma. Al
        // reactivarla, nadie podría administrarla.
        Assert.Equal(["Organización suspendida con única Dueña"], await SoleOwnerOrganizationsAsync(client));
        using var response = await AccountJourney.PostAsync(client, "/api/me/deletion",
            new { reason = "Ya no la uso" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Legal.AccountDeletion.LastAdmin", problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(UserStatus.Active, (await services.GetRequiredService<IUserRepository>()
            .GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Fact]
    public async Task Business_owner_can_request_deletion_when_another_active_owner_exists()
    {
        using var client = CreateClient();
        using var secondClient = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var secondAccount = await AccountJourney.RegisterAsync(factory, secondClient, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();
        var business = Tenant.CreateBusiness("Organización con dos Dueños", false);
        business.Activate();
        using (tenantScope.Enter(business.Id))
        {
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var member = Member.Invite(secondAccount.UserId);
                Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                context.Members.Add(member);
                var tenantAdmin = await context.Roles.SingleAsync(role => role.Name == SystemRoles.TenantAdmin, ct);
                context.RoleAssignments.Add(RoleAssignment.Assign(member.Id, tenantAdmin.Id, tenantAdmin.Scope, tenantAdmin.CompanyId, null).Value);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        Assert.Empty(await SoleOwnerOrganizationsAsync(client));

        using var response = await AccountJourney.PostAsync(client, "/api/me/deletion",
            new { reason = "Ya no la uso" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UserStatus.PendingDeletion, (await services.GetRequiredService<IUserRepository>()
            .GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletion_needs_only_the_session_but_operators_cannot_request_it(bool platformOperator)
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
        Assert.Equal(platformOperator ? HttpStatusCode.Forbidden : HttpStatusCode.OK, rejected.StatusCode);
        if (platformOperator)
        {
            using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Legal.AccountDeletion.PlatformOperator", problem!.RootElement.GetProperty("code").GetString());
        }
        await using var checkedScope = factory.Services.CreateAsyncScope();
        Assert.Equal(platformOperator ? UserStatus.Active : UserStatus.PendingDeletion, (await checkedScope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdAsync(account.UserId, Ct))!.Status);
    }

    [Fact]
    public async Task Request_commits_grace_and_revokes_consumer_business_and_cookie_sessions()
    {
        using var client = CreateClient();
        using var secondClient = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var secondOwner = await AccountJourney.RegisterAsync(factory, secondClient, Ct);
        var consumerToken = client.DefaultRequestHeaders.Authorization!.Parameter;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var business = Tenant.CreateBusiness("Empresa baja HTTP", false);
        business.Activate();
        using (services.GetRequiredService<ITenantScope>().Enter(business.Id))
        {
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            await work.ExecuteInTransactionAsync(async ct =>
            {
                var member = Member.Invite(secondOwner.UserId);
                Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                context.Members.Add(member);
                var tenantAdmin = await context.Roles.SingleAsync(role => role.Name == SystemRoles.TenantAdmin, ct);
                context.RoleAssignments.Add(RoleAssignment.Assign(member.Id, tenantAdmin.Id, tenantAdmin.Scope, tenantAdmin.CompanyId, null).Value);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        var businessToken = client.DefaultRequestHeaders.Authorization!.Parameter;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        using var requested = await AccountJourney.PostAsync(client, "/api/me/deletion",
            new { reason = "Ya no la uso" }, Ct);
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

    private static async Task<string[]> SoleOwnerOrganizationsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. json.RootElement.GetProperty("soleOwnerOrganizations").EnumerateArray().Select(name => name.GetString()!)];
    }

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
