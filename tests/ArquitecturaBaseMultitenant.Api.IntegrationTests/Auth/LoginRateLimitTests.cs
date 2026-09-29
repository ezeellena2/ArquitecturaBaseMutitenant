using System.Globalization;
using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class LoginRateLimitTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/test/login-code-rate-limit", "RateLimiting:LoginCodePermitLimit")]
    [InlineData("/test/login-verify-rate-limit", "RateLimiting:LoginVerifyPermitLimit")]
    public async Task Login_rate_limit_is_partitioned_by_ip_and_returns_retry_after(
        string path, string limitKey)
    {
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting(limitKey, "1"));
        using var client = limited.CreateClient();

        using var allowed = await client.GetAsync(path, TestContext.Current.CancellationToken);
        using var rejected = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Http.TooManyRequests", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        var seconds = body.RootElement.GetProperty("retryAfter").GetInt32();
        Assert.True(seconds > 0);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out var values));
        Assert.Equal(seconds.ToString(CultureInfo.InvariantCulture), Assert.Single(values));
    }
}
