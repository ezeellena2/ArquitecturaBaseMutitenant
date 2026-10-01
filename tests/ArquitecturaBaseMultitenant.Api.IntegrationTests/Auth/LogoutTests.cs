using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba que salir dirija a la página pública inicial. Evita continuar en el destino del acceso que se
/// acaba de cerrar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LogoutTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Logout_redirects_to_the_public_home_instead_of_the_previous_access()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Invitation.Continuation.v1");
        var protectedContinuation = protector.Protect(JsonSerializer.Serialize(new InvitationContinuation(
            new string('B', 43), new string('A', 43), TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(3))));
        client.DefaultRequestHeaders.Add("Cookie", $"__Host-MtInvitation={protectedContinuation}");

        using var response = await client.GetAsync(
            "/connect/logout?client_id=web&post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A5174%2F", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://localhost:5174/", response.Headers.Location?.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie =>
            cookie.StartsWith("__Host-MtInvitation=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invitation_logout_preserves_the_protected_continuation_and_rotates_its_nonce()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var token = new string('B', 43);
        var nonce = new string('A', 43);
        var expiry = TimeProvider.System.GetUtcNow().UtcDateTime.AddMinutes(3);
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Invitation.Continuation.v1");
        var protectedContinuation = protector.Protect(JsonSerializer.Serialize(
            new InvitationContinuation(token, nonce, expiry)));
        client.DefaultRequestHeaders.Add("Cookie", $"__Host-MtInvitation={protectedContinuation}");

        using var response = await client.GetAsync(
            "/connect/logout?client_id=web&post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Finvitacion&preserveInvitation=true", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://localhost:5174/invitacion", response.Headers.Location?.ToString());
        var continuationCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("__Host-MtInvitation=", StringComparison.Ordinal));
        var rotatedProtected = continuationCookie.Split(';', 2)[0].Split('=', 2)[1];
        var rotated = JsonSerializer.Deserialize<InvitationContinuation>(protector.Unprotect(rotatedProtected));
        Assert.NotNull(rotated);
        Assert.Equal(token, rotated.Token);
        Assert.NotEqual(nonce, rotated.Nonce);
        Assert.Equal(expiry, rotated.ExpiresAtUtc);
    }
}
