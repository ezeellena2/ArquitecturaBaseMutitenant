using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Importa claims Google reales del adaptador, sólo en la identidad autorizada y en campos vacíos.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class GoogleNamesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    public async Task Google_registration_imports_compound_given_and_family_names(string access)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = Identity();
        using var host = HostWith(identity);
        using var client = Client(host);
        await ChallengeAsync(client, access, signup: true);
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var user = await ReadGoogleUserAsync(host, identity.Subject);
        AssertNames(user, "María José", "de la Cruz", 1);
    }

    [Theory]
    [InlineData("normalized")]
    [InlineData("overflow")]
    [InlineData("absent")]
    public async Task Google_claims_are_normalized_individually_without_inferring_from_the_combined_claim(string variant)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var identity = Identity() with
        {
            GivenName = variant switch { "normalized" => "  Mari\u0301a  José  ", "overflow" => new string('N', 99) + "😀", _ => null },
            FamilyName = variant switch { "normalized" => " de  la Cruz ", "overflow" => new string('A', 100), _ => null },
        };
        using var host = HostWith(identity);
        using var client = Client(host);
        await ChallengeAsync(client, "consumer", signup: true);
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        var first = variant switch { "normalized" => "María José", "overflow" => new string('N', 99), _ => null };
        var last = variant switch { "normalized" => "de la Cruz", "overflow" => new string('A', 100), _ => null };
        AssertNames(await ReadGoogleUserAsync(host, identity.Subject), first, last, variant == "absent" ? 0u : 1u);
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("deletion")]
    [InlineData("operator")]
    [InlineData("locked")]
    public async Task Rejected_login_never_imports_names_or_advances_the_profile(string rejection)
    {
        var identity = Identity();
        using var host = HostWith(identity);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await SetNamesAsync(host, account.UserId, null, null, identity.Subject);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var user = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == account.UserId, ct);
                var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
                switch (rejection)
                {
                    case "suspended": Assert.True(user.Suspend().IsSuccess); break;
                    case "deletion": Assert.True(user.RequestDeletion("Prueba", now.UtcDateTime, 30).IsSuccess); break;
                    case "operator": user.GrantPlatformOperator(); break;
                    case "locked": user.LockoutEnabled = true; user.LockoutEnd = now.AddMinutes(10); break;
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await ChallengeAsync(client, "consumer");
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Contains("error=", callback.Headers.Location!.OriginalString, StringComparison.Ordinal);
        AssertNames(await ReadUserAsync(host, account.UserId), null, null, 0);
    }

    [Theory]
    [InlineData("subject", null, null)]
    [InlineData("subject", "Ana", null)]
    [InlineData("subject", null, "Pérez")]
    [InlineData("subject", "Ana", "Pérez")]
    [InlineData("email", null, null)]
    [InlineData("email", "Ana", null)]
    [InlineData("email", null, "Pérez")]
    [InlineData("email", "Ana", "Pérez")]
    [InlineData("link", null, null)]
    [InlineData("link", "Ana", null)]
    [InlineData("link", null, "Pérez")]
    [InlineData("link", "Ana", "Pérez")]
    public async Task Login_and_link_fill_only_missing_parts_and_repetition_keeps_profile_version(
        string flow, string? firstName, string? lastName)
    {
        var identity = Identity();
        using var host = HostWith(identity);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct,
            Domain.ValueObjects.Email.Create(identity.Email).Value);
        await SetNamesAsync(host, account.UserId, firstName, lastName, flow == "subject" ? identity.Subject : null);
        var expectedVersion = firstName is null || lastName is null ? 1u : 0u;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            await ChallengeAsync(client, "consumer", link: flow == "link");
            using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.DoesNotContain("error=", callback.Headers.Location!.OriginalString, StringComparison.Ordinal);
            AssertNames(await ReadUserAsync(host, account.UserId), firstName ?? "María José", lastName ?? "de la Cruz", expectedVersion);
        }
        // Una edición que leyó la versión anterior a la importación no puede pisar el nombre nuevo.
        if (expectedVersion > 0)
        {
            using var stale = await client.PutAsJsonAsync("/api/me", new
                { displayName = "Viejo", culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version = 0 }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_preference_save_preserves_Google_partition_and_full_combined_name(bool longNames)
    {
        var identity = Identity() with
        {
            GivenName = longNames ? new string('N', 95) + " José" : "María José",
            FamilyName = longNames ? new string('A', 100) : "de la Cruz",
        };
        using var host = HostWith(identity);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct,
            Domain.ValueObjects.Email.Create(identity.Email).Value);
        await ChallengeAsync(client, "consumer");
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        var user = await ReadUserAsync(host, account.UserId);
        AssertNames(user, identity.GivenName, identity.FamilyName, 1);
        using var saved = await client.PutAsJsonAsync("/api/me", new
            { displayName = user.DisplayName, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version = user.ProfileVersion }, Ct);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertNames(await ReadUserAsync(host, account.UserId), identity.GivenName, identity.FamilyName, 2);
        using var invalid = await client.PutAsJsonAsync("/api/me", new
            { displayName = new string('N', 101), culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version = 2 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = await invalid.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Users.User.InvalidDisplayName", problem!.RootElement.GetProperty("code").GetString());
        AssertNames(await ReadUserAsync(host, account.UserId), identity.GivenName, identity.FamilyName, 2);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("email")]
    [InlineData("link")]
    public async Task Unverified_email_never_imports_names_even_for_a_known_subject(string flow)
    {
        var identity = Identity() with { EmailVerified = false };
        using var host = HostWith(identity);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct,
            Domain.ValueObjects.Email.Create(identity.Email).Value);
        await SetNamesAsync(host, account.UserId, null, null, flow == "subject" ? identity.Subject : null);
        await ChallengeAsync(client, "consumer", link: flow == "link");
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        AssertNames(await ReadUserAsync(host, account.UserId), null, null, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Link_rejected_for_foreign_subject_or_session_keeps_both_profiles(bool foreignSession)
    {
        var identity = Identity();
        using var host = HostWith(identity);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        using var otherClient = Client(host);
        var other = await AccountJourney.RegisterAsync(host, otherClient, Ct);
        if (!foreignSession) await SetNamesAsync(host, other.UserId, null, null, identity.Subject);
        await ChallengeAsync(client, "consumer", link: true);
        if (foreignSession) await AccountJourney.RegisterAsync(host, client, Ct);
        using var callback = await client.GetAsync("/api/auth/external/callback", Ct);
        Assert.Contains("error=", callback.Headers.Location!.OriginalString, StringComparison.Ordinal);
        AssertNames(await ReadUserAsync(host, account.UserId), null, null, 0);
        AssertNames(await ReadUserAsync(host, other.UserId), null, null, 0);
    }

    private static GoogleIdentity Identity() => new(Guid.NewGuid().ToString("N"),
        $"google-names-{Guid.NewGuid():N}@example.test", "María José", "de la Cruz");

    private WebApplicationFactory<Program> HostWith(GoogleIdentity identity) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Authentication:Google:ClientId", string.Empty);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(identity);
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, GoogleHandler>(GoogleDefaults.AuthenticationScheme, _ => { });
        });
    });

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new()
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task ChallengeAsync(HttpClient client, string access, bool signup = false, bool link = false)
    {
        using var tokenResponse = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
        using var token = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var requestToken = token!.RootElement.GetProperty("requestToken").GetString()!;
        if (link)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/me/external/google");
            request.Headers.Add("RequestVerificationToken", requestToken);
            using var response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        else if (signup)
        {
            using var response = await client.PostAsync("/api/auth/external/google", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["signup"] = "true", ["acceptedTerms"] = "true", ["access"] = access,
                ["returnTo"] = "/", ["culture"] = "es-AR", ["timeZoneId"] = "America/Argentina/Buenos_Aires",
                ["__RequestVerificationToken"] = requestToken,
            }), Ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        else
        {
            using var response = await client.GetAsync("/api/auth/external/google?returnUrl=%2Finvitacion", Ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
    }

    private static async Task SetNamesAsync(WebApplicationFactory<Program> host, Guid userId, string? first, string? last, string? subject)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(row => row.Id == userId, ct);
            user.Rename(first is null ? last : last is null ? first : first + " " + last);
            db.Entry(user).Property(row => row.FirstName).CurrentValue = first;
            db.Entry(user).Property(row => row.LastName).CurrentValue = last;
            if (subject is not null)
            {
                var method = LoginMethod.CreateGoogle(userId, subject);
                method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<ApplicationUser> ReadGoogleUserAsync(WebApplicationFactory<Program> host, string subject)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.LoginMethods.Where(row => row.Value == subject).Select(row => row.UserId).SingleAsync(Ct);
        return await db.Users.AsNoTracking().SingleAsync(row => row.Id == userId, Ct);
    }

    private static async Task<ApplicationUser> ReadUserAsync(WebApplicationFactory<Program> host, Guid userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking().SingleAsync(row => row.Id == userId, Ct);
    }

    private static void AssertNames(ApplicationUser user, string? first, string? last, uint version)
    {
        Assert.Equal(first, user.FirstName);
        Assert.Equal(last, user.LastName);
        Assert.Equal(first is null ? last : last is null ? first : first + " " + last, user.DisplayName);
        Assert.Equal(version, user.ProfileVersion);
    }

    private sealed record GoogleIdentity(string Subject, string Email, string? GivenName, string? FamilyName, bool EmailVerified = true);

    private sealed class GoogleHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, GoogleIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            List<Claim> claims = [new(ClaimTypes.NameIdentifier, identity.Subject), new(ClaimTypes.Email, identity.Email),
                new(ClaimTypes.Name, "Combinado distinto"), new("email_verified", identity.EmailVerified ? "true" : "false")];
            if (identity.GivenName is { } first) claims.Add(new(ClaimTypes.GivenName, first));
            if (identity.FamilyName is { } last) claims.Add(new(ClaimTypes.Surname, last));
            await Context.SignInAsync(IdentityConstants.ExternalScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, GoogleDefaults.AuthenticationScheme)), properties);
            Response.Redirect(properties.RedirectUri ?? "/api/auth/external/callback");
        }
    }
}
