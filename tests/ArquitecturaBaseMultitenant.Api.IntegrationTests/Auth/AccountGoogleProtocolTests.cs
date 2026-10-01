using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba la protección del desafío para vincular Google a una cuenta existente. Exige antiforgery,
/// prueba de titularidad y estado OAuth ligado a la cuenta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountGoogleProtocolTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_open_session_and_antiforgery_cannot_link_Google_without_reauthentication()
    {
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "integration-client");
            builder.UseSetting("Authentication:Google:ClientSecret", "integration-client-secret");
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var antiforgery = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
        using var token = await antiforgery.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/external/google") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("RequestVerificationToken", token!.RootElement.GetProperty("requestToken").GetString());
        using var started = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, started.StatusCode);
        Assert.False(started.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Challenge_requires_antiforgery_and_protects_the_bearer_account_in_OAuth_state()
    {
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "integration-client");
            builder.UseSetting("Authentication:Google:ClientSecret", "integration-client-secret");
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        using var rejected = await client.PostAsJsonAsync("/api/me/external/google", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var antiforgery = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
        using var token = await antiforgery.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        await using var scope = host.Services.CreateAsyncScope();
        var reauthTicket = await AccountJourney.IssueReauthTicketAsync(scope.ServiceProvider, account.UserId, ReauthAction.LinkGoogle, Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/external/google")
            { Content = JsonContent.Create(new { reauthTicket, userId = Guid.NewGuid() }) };
        request.Headers.Add("RequestVerificationToken", token!.RootElement.GetProperty("requestToken").GetString());
        using var started = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        using var body = await started.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var url = new Uri(body!.RootElement.GetProperty("redirectUrl").GetString()!);
        Assert.Equal("accounts.google.com", url.Host);
        var state = QueryHelpers.ParseQuery(url.Query)["state"].ToString();
        var options = host.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get(GoogleDefaults.AuthenticationScheme);
        var protectedState = options.StateDataFormat.Unprotect(state);
        Assert.NotNull(protectedState);
        Assert.Equal(account.UserId.ToString("D"), protectedState.Items[GoogleAccountLinkState.UserIdKey]);
        Assert.Equal(GoogleAccountLinkState.CallbackPath, protectedState.RedirectUri);
        Assert.Contains(started.Headers.GetValues("Set-Cookie"), cookie => cookie.Contains("Correlation", StringComparison.Ordinal));
    }
}
