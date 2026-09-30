using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
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

[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionLoginTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
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
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var database = services.GetRequiredService<ApplicationDbContext>();
            var secrets = services.GetRequiredService<ISecureTokenGenerator>();
            var secret = secrets.Generate();
            var method = await database.LoginMethods.SingleAsync(row => row.UserId == account.UserId && row.IsPrimary, Ct);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                database.ReauthTickets.Add(ReauthTicket.Issue(account.UserId, ReauthAction.DeleteAccount,
                    method.Id, null, secrets.Hash(secret), clock.GetUtcNow().UtcDateTime).Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            using var deletion = await AccountJourney.PostAsync(client, "/api/me/deletion",
                new { reason = "Prueba de gracia", reauthTicket = secret }, Ct);
            Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = account.Email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, account.Email.Value, Ct);
        var returnUrl = "/connect/authorize?client_id=web&access=" + access;
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
        var ticket = await checkedServices.GetRequiredService<ApplicationDbContext>().ReauthTickets.AsNoTracking()
            .SingleAsync(row => row.TokenHash == checkedServices.GetRequiredService<ISecureTokenGenerator>().Hash(cancelTicket!), Ct);
        Assert.Equal(account.UserId, ticket.UserId);
        Assert.Equal(ReauthAction.CancelDeletion, ticket.Action);
        Assert.Equal(returnUrl, ticket.ReturnUrl);
        Assert.Equal(TimeSpan.FromMinutes(5), ticket.ExpiresAtUtc - ticket.IssuedAtUtc);
    }
}
