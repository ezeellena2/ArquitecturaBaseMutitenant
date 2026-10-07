using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>La sesión autoriza los cambios de Cuenta; el método nuevo prueba su posesión y los cambios avisan.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountSessionActionsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public async Task Adding_email_sends_only_its_own_code_and_notifies_existing_methods_after_verification()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var email = "added-" + Guid.NewGuid().ToString("N") + "@example.test";
        var before = await OutboxIdsAsync();
        using var added = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
        using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var methodId = body!.RootElement.GetProperty("methodId").GetGuid();
        Assert.Single(await EmailsToAsync(email, before));
        Assert.Empty(await EmailsToAsync(account.Email.Value, before));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var issued = Assert.Single(await db.LoginCodes.AsNoTracking()
                .Where(code => code.RequestedByUserId == account.UserId).ToArrayAsync(Ct));
            Assert.Equal(LoginCodePurpose.VerifyDestination, issued.Purpose);
            Assert.Equal(email, issued.Destination);
            Assert.False(await db.LoginMethods.AnyAsync(method => method.Id == methodId && method.VerifiedAtUtc != null, Ct));
        }
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(client, $"/api/me/login-methods/{methodId:D}/verify", new { code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains("agreg", Assert.Single(await EmailsToAsync(account.Email.Value, before)).TextBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Primary_and_removal_need_no_ticket_and_keep_a_verified_primary_and_the_last_method()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var second = LoginMethod.CreateEmail(account.UserId,
            Email.Create("second-" + Guid.NewGuid().ToString("N") + "@example.test").Value);
        Guid originalId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            originalId = (await services.GetRequiredService<ILoginMethodReader>().ListByUserIdAsync(account.UserId, Ct)).Single().Id;
            second.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ILoginMethodRepository>().Add(second);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        var before = await OutboxIdsAsync();
        using var primary = await client.PutAsJsonAsync($"/api/me/login-methods/{second.Id:D}/primary", new { }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, primary.StatusCode);
        Assert.Single(await EmailsToAsync(account.Email.Value, before));
        before = await OutboxIdsAsync();
        using var removed = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/me/login-methods/{second.Id:D}")
            { Content = JsonContent.Create(new { }) }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Single(await EmailsToAsync(account.Email.Value, before));
        using var last = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/me/login-methods/{originalId:D}")
            { Content = JsonContent.Create(new { }) }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
        using var problem = await last.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LoginMethodErrors.LastMethod.Code, problem!.RootElement.GetProperty("code").GetString());
        await using var checkedScope = factory.Services.CreateAsyncScope();
        var db = checkedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var remaining = Assert.Single(await db.LoginMethods.AsNoTracking().Where(method => method.UserId == account.UserId).ToArrayAsync(Ct));
        Assert.Equal(originalId, remaining.Id);
        Assert.True(remaining.IsPrimary);
        Assert.Equal(remaining.Value, (await db.Users.AsNoTracking().SingleAsync(user => user.Id == account.UserId, Ct)).Email);
    }

    [Fact]
    public async Task Deletion_needs_no_ticket_and_preserves_thirty_days_notice_and_revocation()
    {
        using var client = CreateClient();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var before = await OutboxIdsAsync();
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        using var requested = await AccountJourney.PostAsync(client, "/api/me/deletion", new { reason = "Ya no la uso" }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        using var response = await requested.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.InRange(response!.RootElement.GetProperty("scheduledForUtc").GetDateTime(), nowUtc.AddDays(30), nowUtc.AddDays(30).AddMinutes(1));
        Assert.Single(await EmailsToAsync(account.Email.Value, before));
        using var denied = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    private async Task<Guid[]> OutboxIdsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Select(message => message.Id).ToArrayAsync(Ct);
    }

    private async Task<EmailMessage[]> EmailsToAsync(string to, Guid[] before)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var protector = services.GetRequiredService<IPayloadProtector>();
        var payloads = await services.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(message => message.Channel == "email" && !before.Contains(message.Id))
            .Select(message => message.EncryptedPayload).ToArrayAsync(Ct);
        return [.. payloads.Select(payload => JsonSerializer.Deserialize<EmailMessage>(protector.Unprotect(payload))!)
            .Where(message => string.Equals(message.To, to, StringComparison.OrdinalIgnoreCase))];
    }
}
