using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.RequestContext;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba los adaptadores de sesión, usuario actual y datos de la petición. Exige identidad y acceso
/// obtenidos de claims y una configuración coherente del origen público.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SignInServiceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Current_user_reads_subject_and_access_from_claims_only()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId.ToString("D")), new Claim("access", "business")], "test"));
        var current = new CurrentUser(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(userId, current.UserId);
        Assert.Equal(Access.Business, current.Access);

        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")), new Claim("access", "unknown")], "test"));
        Assert.Equal(userId, current.UserId);
        Assert.Null(current.Access);
    }

    [Fact]
    public void Request_info_uses_processed_remote_address_and_does_not_log_it()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.8");
        context.Request.Headers.UserAgent = "Test Browser";
        var info = new RequestInfo(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("203.0.113.8", info.IpAddress);
        Assert.Equal("Test Browser", info.UserAgent);
        Assert.DoesNotContain("ILogger", typeof(RequestInfo).GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name));
    }

    [Fact]
    public void Public_origin_follows_issuer_and_google_availability_follows_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Issuer"] = "https://example.test/app",
        }).Build();

        Assert.Equal("https://example.test/app/", new PublicOrigin(configuration).Value?.AbsoluteUri);
        Assert.True(new GoogleAvailability(true).IsEnabled);
        Assert.False(new GoogleAvailability(false).IsEnabled);
        Assert.Null(new PublicOrigin(new ConfigurationBuilder().Build()).Value);
    }

    [Fact]
    public void Sign_in_port_stays_small()
    {
        Assert.True(typeof(ISignInService).GetMembers().Length <= 12);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public async Task External_scheme_claim_controls_email_verification(bool? claim, bool expected)
    {
        using var client = factory.CreateClient();
        using var ticket = await client.PostAsJsonAsync("/test/auth/external-ticket",
            new { emailVerified = claim }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, ticket.StatusCode);

        using var response = await client.GetAsync("/test/auth/external-info", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<ExternalLoginInfo>(Ct);
        Assert.Equal("Google", login?.Provider);
        Assert.Equal(expected, login?.EmailVerified);
    }

    [Fact]
    public async Task Global_lookup_finds_only_verified_login_methods()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var user = ApplicationUser.Create(null, "es-AR", "America/Argentina/Buenos_Aires").Value;
        var email = Email.Create("lookup@example.test").Value;
        var verified = LoginMethod.CreateEmail(user.Id, email);
        Assert.True(verified.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
        var unverified = LoginMethod.CreateGoogle(user.Id, "google-subject-unverified");

        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(user);
            context.LoginMethods.AddRange(verified, unverified);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);

        var lookup = new UserLookup(context);
        Assert.Equal(user.Id, await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, email.Value, Ct));
        Assert.Null(await lookup.FindVerifiedUserIdAsync(LoginMethodType.Google, unverified.Value, Ct));
        Assert.Null(await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, "absent@example.test", Ct));
    }

    [Fact]
    public async Task Technical_sign_in_writes_require_transaction_and_cookie_waits_for_commit()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var service = new SignInService(null!, null!, null!, services.GetRequiredService<ApplicationDbContext>());
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterFailedAttemptAsync(userId, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetFailedAttemptsAsync(userId, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RevokeSessionsAsync(userId, Ct));

        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SignInAsync(userId, ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private sealed record ExternalLoginInfo(string Provider, bool EmailVerified);
}
