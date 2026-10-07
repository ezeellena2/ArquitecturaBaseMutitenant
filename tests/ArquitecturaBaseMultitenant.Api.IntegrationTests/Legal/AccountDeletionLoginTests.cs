using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>
/// Comprueba la prueba de ingreso de una cuenta con baja pendiente y su cancelación. Conserva el retorno
/// elegido y evita emitir sesión o reutilizar la prueba antes de cancelar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionLoginTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_proof_from_another_account_cannot_cancel_the_requested_account_or_change_its_return()
    {
        using var provedClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var proved = await AccountJourney.RegisterAsync(factory, provedClient, Ct);
        var other = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tokens = services.GetRequiredService<ISecureTokenGenerator>();
        var secret = tokens.Generate();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            await users.RequestDeletionAsync(proved.UserId, "Prueba", now, 30, ct);
            await users.RequestDeletionAsync(other.UserId, "Prueba", now, 30, ct);
            services.GetRequiredService<IAccountDeletionCancelTicketRepository>().Add(
                AccountDeletionCancelTicket.Issue(proved.UserId, tokens.Hash(secret), now, "/invitacion"));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        using var cancelled = await AccountJourney.PostAsync(otherClient, "/api/auth/deletion/cancel",
            new { cancelTicket = secret, userId = other.UserId, returnUrl = "//other.test" }, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var body = await cancelled.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("/invitacion", body!.RootElement.GetProperty("returnUrl").GetString());
        var db = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Users.UserStatus.Active,
            (await db.Users.AsNoTracking().SingleAsync(user => user.Id == proved.UserId, Ct)).Status);
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Users.UserStatus.PendingDeletion,
            (await db.Users.AsNoTracking().SingleAsync(user => user.Id == other.UserId, Ct)).Status);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("invitation")]
    public async Task Code_proves_pending_account_without_session_and_preserves_the_selected_door(string access)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var clock = new FakeTimeProvider(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        clock.Advance(TimeSpan.FromMinutes(2));
        using (var deletion = await AccountJourney.PostAsync(client, "/api/me/deletion", new { reason = "Prueba de gracia" }, Ct))
            Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, account.Email.Value, Ct);
        var returnUrl = access == "invitation" ? "/invitacion" : "/connect/authorize?client_id=web&access=" + access;
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = account.Email.Value, code, returnUrl }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var body = problem!.RootElement;
        Assert.Equal("Identity.Account.PendingDeletion", body.GetProperty("code").GetString());
        var cancelTicket = body.GetProperty("cancelTicket").GetString();
        Assert.True(ISecureTokenGenerator.HasTokenFormat(cancelTicket));
        Assert.Equal(returnUrl, body.GetProperty("returnUrl").GetString());
        Assert.True(body.GetProperty("scheduledForUtc").GetDateTime() > clock.GetUtcNow().UtcDateTime);
        Assert.False(verified.Headers.Contains("Set-Cookie"));
        await using var checkedScope = host.Services.CreateAsyncScope();
        var checkedServices = checkedScope.ServiceProvider;
        var ticket = await checkedServices.GetRequiredService<ApplicationDbContext>().AccountDeletionCancelTickets.AsNoTracking()
            .SingleAsync(row => row.TokenHash == checkedServices.GetRequiredService<ISecureTokenGenerator>().Hash(cancelTicket!), Ct);
        Assert.Equal(account.UserId, ticket.UserId);
        Assert.Equal(returnUrl, ticket.ReturnUrl);
        Assert.Equal(TimeSpan.FromMinutes(5), ticket.ExpiresAtUtc - ticket.IssuedAtUtc);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("invitation")]
    [InlineData("platform")]
    public async Task Cancellation_uses_the_proved_account_and_return_door_and_rejects_replay(string access)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        if (access == "platform")
            await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, Ct);
        var oldToken = client.DefaultRequestHeaders.Authorization;
        var returnUrl = access == "invitation" ? "/invitacion" : "/connect/authorize?client_id=web&access=" + access;
        string secret;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var secrets = services.GetRequiredService<ISecureTokenGenerator>();
            secret = secrets.Generate();
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await services.GetRequiredService<IUserRepository>().RequestDeletionAsync(account.UserId, "Prueba", nowUtc, 30, ct);
                await services.GetRequiredService<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity.ISignInService>()
                    .RevokeSessionsAsync(account.UserId, ct);
                services.GetRequiredService<IAccountDeletionCancelTicketRepository>().Add(AccountDeletionCancelTicket.Issue(account.UserId, secrets.Hash(secret), nowUtc, returnUrl));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var cancelled = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var body = await cancelled.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(returnUrl, body!.RootElement.GetProperty("returnUrl").GetString());
        var cookie = access == "platform" ? ".AspNetCore.Identity.Platform=" : ".AspNetCore.Identity.Application=";
        Assert.Contains(cancelled.Headers.GetValues("Set-Cookie"), value => value.StartsWith(cookie, StringComparison.Ordinal));
        using var replay = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        client.DefaultRequestHeaders.Authorization = oldToken;
        using var oldSession = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        await using var checkedScope = factory.Services.CreateAsyncScope();
        var row = await checkedScope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct);
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Users.UserStatus.Active, row!.Status);
        Assert.Null(row.DeletionScheduledForUtc);
        Assert.Null((await checkedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
            .SingleAsync(user => user.Id == account.UserId, Ct)).DeletionReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_rejects_expired_ticket_or_expired_grace_without_changing_state(bool expiredTicket)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var secrets = services.GetRequiredService<ISecureTokenGenerator>();
        var secret = secrets.Generate();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await services.GetRequiredService<IUserRepository>().RequestDeletionAsync(account.UserId, "Prueba",
                expiredTicket ? nowUtc : nowUtc.AddDays(-31), 30, ct);
            services.GetRequiredService<IAccountDeletionCancelTicketRepository>().Add(AccountDeletionCancelTicket.Issue(account.UserId, secrets.Hash(secret), expiredTicket ? nowUtc.AddMinutes(-6) : nowUtc,
                "/connect/authorize?client_id=web&access=consumer"));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        client.DefaultRequestHeaders.Authorization = null;
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(expiredTicket ? HttpStatusCode.Forbidden : HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Users.UserStatus.PendingDeletion,
            (await services.GetRequiredService<IUserRepository>().GetByIdAsync(account.UserId, Ct))!.Status);
        var hash = secrets.Hash(secret);
        Assert.Null((await services.GetRequiredService<ApplicationDbContext>().AccountDeletionCancelTickets.AsNoTracking()
            .SingleAsync(ticket => ticket.TokenHash == hash, Ct)).ConsumedAtUtc);
    }
}
