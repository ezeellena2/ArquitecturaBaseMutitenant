using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba los límites de las rutas reales de ingreso por IP y por intentos inválidos. Exige respuestas
/// propias para correos conocidos y desconocidos, conservando el tope por IP (ADR 0043).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RealLoginRateLimitTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("api/auth/login-code", RateLimitProfile.LoginCode)]
    [InlineData("api/auth/login-code/verify", RateLimitProfile.LoginVerify)]
    [InlineData("api/auth/signup", RateLimitProfile.LoginCode)]
    [InlineData("api/auth/signup/verify", RateLimitProfile.LoginVerify)]
    [InlineData("api/account-recovery/preview", RateLimitProfile.LoginCode)]
    public void Real_login_routes_declare_their_rate_limit_profile(string path, RateLimitProfile profile)
    {
        using var client = factory.CreateClient();
        var endpoint = Assert.Single(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>(),
            candidate => candidate.RoutePattern.RawText == path &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        Assert.Equal(profile, endpoint.Metadata.GetMetadata<RateLimitAttribute>()?.Profile);
    }

    [Theory]
    [InlineData("/api/auth/login-code", false)]
    [InlineData("/api/auth/signup", true)]
    public async Task Known_and_unknown_email_get_their_own_real_field_response(string path, bool signup)
    {
        if (signup)
        {
            await factory.Services.SeedDatabaseAsync(Ct);
        }
        using var client = factory.CreateClient();
        var known = $"known-real-{Guid.NewGuid():N}@example.test";
        var unknown = $"unknown-real-{Guid.NewGuid():N}@example.test";
        await CreateKnownAccountAsync(known);

        using var knownResponse = await PostAsync(client, path, known, signup);
        using var unknownResponse = await PostAsync(client, path, unknown, signup);

        Assert.Equal(signup ? HttpStatusCode.BadRequest : HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(signup ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest, unknownResponse.StatusCode);
        var rejected = signup ? knownResponse : unknownResponse;
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(signup ? SignupErrors.EmailTakenCode : LoginCodeErrors.AccountNotFoundCode, problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("email", out _));
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Real_login_verify_locks_out_after_repeated_invalid_codes()
    {
        using var client = factory.CreateClient();
        var address = $"lockout-real-{Guid.NewGuid():N}@example.test";
        await CreateKnownAccountAsync(address);
        await SeedTenAttemptCodeAsync(address);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var rejected = await PostVerifyAsync(client, address);
            Assert.Equal(attempt == 9 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest,
                rejected.StatusCode);
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string email, bool signup)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(signup
                ? (object)new { email, acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" }
                : new { email }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> PostVerifyAsync(HttpClient client, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-code/verify")
        {
            Content = JsonContent.Create(new
            {
                email,
                code = "000000",
                returnUrl = "/connect/authorize?client_id=web",
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private async Task CreateKnownAccountAsync(string address)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = Email.Create(address).Value;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var user = await services.GetRequiredService<IUserRepository>()
                .CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
            var method = LoginMethod.CreateEmail(user.Id, email);
            method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            method.MakePrimary();
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(user.Id, email, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task SeedTenAttemptCodeAsync(string address)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var destination = LoginCodeDestination.ForEmail(Email.Create(address).Value);
        var hash = services.GetRequiredService<ILoginCodeHasher>()
            .Hash(destination, LoginCodePurpose.Login, "123456");
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ILoginCodeRepository>().Add(LoginCode.Issue(destination,
                LoginCodePurpose.Login, null, hash,
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
                TimeSpan.FromMinutes(10), maxAttempts: 10));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }
}
