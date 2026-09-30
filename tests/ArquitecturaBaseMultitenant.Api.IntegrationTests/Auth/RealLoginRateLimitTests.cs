using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class RealLoginRateLimitTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("api/auth/login-code", "login-code")]
    [InlineData("api/auth/login-code/verify", "login-verify")]
    [InlineData("api/auth/signup", "login-code")]
    [InlineData("api/auth/signup/verify", "login-verify")]
    public void Real_login_routes_declare_rate_limit_policies(string path, string policy)
    {
        using var client = factory.CreateClient();
        var endpoint = Assert.Single(factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>(),
            candidate => candidate.RoutePattern.RawText == path &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        Assert.Equal(policy, endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public async Task Real_login_route_limits_each_ip_separately()
    {
        await using var limited = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:LoginCodePermitLimit", "1");
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestIpStartupFilter>());
        });
        using var client = limited.CreateClient();

        using var first = await PostCodeAsync(client, "/api/auth/login-code", "127.0.0.1");
        using var second = await PostCodeAsync(client, "/api/auth/login-code", "127.0.0.2");
        using var rejected = await PostCodeAsync(client, "/api/auth/login-code", "127.0.0.1");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Http.TooManyRequests", body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/api/auth/login-code", false)]
    [InlineData("/api/auth/signup", true)]
    public async Task Known_and_unknown_email_get_the_same_real_request_response(string path, bool signup)
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

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(await knownResponse.Content.ReadAsStringAsync(Ct),
            await unknownResponse.Content.ReadAsStringAsync(Ct));
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

    private static async Task<HttpResponseMessage> PostCodeAsync(HttpClient client, string path, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { email = $"rate-{Guid.NewGuid():N}@example.test" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        request.Headers.Add("X-Test-Remote-IP", ip);
        return await client.SendAsync(request, Ct);
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

    private sealed class TestIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var value) &&
                    IPAddress.TryParse(value.ToString(), out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }
                return following(context);
            });
            next(app);
        };
    }
}
