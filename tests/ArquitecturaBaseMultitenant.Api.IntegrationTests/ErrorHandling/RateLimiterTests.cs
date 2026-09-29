using System.Globalization;
using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimiterTests(ApiFactory factory)
{
    [Fact]
    public async Task Rejected_request_has_problem_and_matching_retry_header()
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<RateLimiterOptions>(options => options.AddFixedWindowLimiter("test-rejection", limiter =>
            {
                limiter.PermitLimit = 1;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = false;
            }))));
        using var client = limited.CreateClient();

        using var allowed = await client.GetAsync("/test/rate-limit", TestContext.Current.CancellationToken);
        using var rejected = await client.GetAsync("/test/rate-limit", TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Http.TooManyRequests", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        var seconds = body.RootElement.GetProperty("retryAfter").GetInt32();
        Assert.True(seconds > 0);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var values));
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), Assert.Single(values));
    }
}
