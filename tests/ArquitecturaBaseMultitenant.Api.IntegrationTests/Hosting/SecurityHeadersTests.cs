using System.Net;
using ArquitecturaBaseMultitenant.Api.Hosting;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Hosting;

/// <summary>
/// Comprueba encabezados de seguridad en respuestas exitosas y errores. También verifica qué datos puede
/// reenviar un proxy confiable.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SecurityHeadersTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/alive", HttpStatusCode.OK)]
    [InlineData("/api/no-existe", HttpStatusCode.NotFound)]
    [InlineData("/test/throw", HttpStatusCode.InternalServerError)]
    public async Task Success_and_error_responses_include_security_headers(string path, HttpStatusCode status)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("form-action 'self' https://accounts.google.com", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trusted_proxy_sets_scheme_and_client_ip_but_never_forwards_host()
    {
        await using var api = CreateApi("10.0.0.4", ("ForwardedHeaders:TrustAll", "true"));
        using var client = CreateClient(api, "http://localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/no-existe");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.1, 203.0.113.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("https", Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        Assert.Equal("203.0.113.10", Assert.Single(response.Headers.GetValues("X-Test-RemoteIp")));
        Assert.Equal("localhost", Assert.Single(response.Headers.GetValues("X-Test-Host")));
    }

    [Fact]
    public async Task Untrusted_proxy_headers_are_ignored()
    {
        await using var api = CreateApi("198.51.100.200");
        using var client = CreateClient(api, "https://localhost");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/no-existe");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("https", Assert.Single(response.Headers.GetValues("X-Test-Scheme")));
        Assert.Equal("198.51.100.200", Assert.Single(response.Headers.GetValues("X-Test-RemoteIp")));
    }

    [Fact]
    public void Forwarded_headers_accept_only_one_hop_and_only_for_and_proto()
    {
        using var services = CreateServices(
            ("ForwardedHeaders:KnownProxies:0", "203.0.113.7"),
            ("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8"));

        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(IPAddress.Parse("203.0.113.7"), options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
    }

    [Fact]
    public void Trust_all_is_explicit_and_cannot_combine_with_an_allowlist()
    {
        using var trustAll = CreateServices(("ForwardedHeaders:TrustAll", "true"));
        var options = trustAll.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);

        using var conflicting = CreateServices(
            ("ForwardedHeaders:TrustAll", "true"),
            ("ForwardedHeaders:KnownProxies:0", "203.0.113.7"));
        var exception = Assert.Throws<InvalidOperationException>(
            () => conflicting.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);
        Assert.Contains("TrustAll", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "not-an-ip")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "not-a-network")]
    public void Invalid_proxy_allowlist_entries_fail_fast(string key, string value)
    {
        using var services = CreateServices((key, value));

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> CreateApi(
        string remoteIpAddress, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(remoteIpAddress))));
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> api, string baseAddress) =>
        api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(baseAddress),
            AllowAutoRedirect = false,
        });

    private static ServiceProvider CreateServices(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddTrustedForwardedHeaders(configuration);

        return services.BuildServiceProvider();
    }

    private sealed class RemoteIpStartupFilter(IPAddress remoteIpAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, following) =>
            {
                context.Connection.RemoteIpAddress = remoteIpAddress;
                context.Response.OnStarting(static state =>
                {
                    var request = (HttpContext)state;
                    request.Response.Headers["X-Test-Scheme"] = request.Request.Scheme;
                    request.Response.Headers["X-Test-RemoteIp"] = request.Connection.RemoteIpAddress?.ToString();
                    request.Response.Headers["X-Test-Host"] = request.Request.Host.Host;
                    return Task.CompletedTask;
                }, context);
                await following();
            });
            next(app);
        };
    }
}
