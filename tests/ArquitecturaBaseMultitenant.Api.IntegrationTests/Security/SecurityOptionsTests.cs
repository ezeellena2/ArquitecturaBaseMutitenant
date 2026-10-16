using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Configuration.Security;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Security;

/// <summary>
/// Las opciones de seguridad de E9 conservan los valores de siempre (tokens, cookies, idempotencia, enlace, bajas y cuotas
/// de arranque), el host los aplica, y un valor fuera de rango impide arrancar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class SecurityOptionsTests(ApiFactory factory)
{
    [Fact]
    public void Defaults_keep_the_values_the_product_already_had()
    {
        var lifetimes = new SessionLifetimeOptions();
        var idempotency = new IdempotencyOptions();
        var deletion = new AccountDeletionOptions();

        Assert.Equal((5, 15, 30, 30), (lifetimes.AuthorizationCodeMinutes, lifetimes.AccessTokenMinutes,
            lifetimes.RefreshTokenDays, lifetimes.BrowserCookieDays));
        Assert.Equal((24, 60), (idempotency.ValidityHours, idempotency.CleanupIntervalMinutes));
        Assert.Equal((100, 60), (deletion.BatchSize, deletion.IntervalMinutes));
        Assert.Equal(10, new LoginLinkOptions().LifetimeMinutes);
        Assert.Equal(PlatformRateLimits.Initial, new RateLimitOptions().ToLimits());
        Assert.Equal(new RateLimitQuota(120, 60), new RateLimitOptions().ToLimits().WhatsAppWebhook);
    }

    [Fact]
    public void The_host_applies_them_to_tokens_and_browser_cookies()
    {
        var server = factory.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        Assert.Equal(TimeSpan.FromMinutes(5), server.AuthorizationCodeLifetime);
        Assert.Equal(TimeSpan.FromMinutes(15), server.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), server.RefreshTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), cookies.Get(IdentityConstants.ApplicationScheme).ExpireTimeSpan);
        Assert.Equal(TimeSpan.FromDays(30), cookies.Get(BrowserSessionKeys.PlatformScheme).ExpireTimeSpan);
    }

    [Theory]
    [InlineData("Authentication:Lifetimes:AuthorizationCodeMinutes", "11", "AuthorizationCodeMinutes")]
    [InlineData("Authentication:Lifetimes:AccessTokenMinutes", "0", "AccessTokenMinutes")]
    [InlineData("Authentication:Lifetimes:RefreshTokenDays", "91", "RefreshTokenDays")]
    [InlineData("Authentication:Lifetimes:BrowserCookieDays", "0", "BrowserCookieDays")]
    [InlineData("Authentication:LoginLink:LifetimeMinutes", "61", "LifetimeMinutes")]
    [InlineData("Idempotency:ValidityHours", "0", "ValidityHours")]
    [InlineData("Idempotency:CleanupIntervalMinutes", "1441", "CleanupIntervalMinutes")]
    [InlineData("AccountDeletion:BatchSize", "0", "BatchSize")]
    [InlineData("AccountDeletion:IntervalMinutes", "0", "IntervalMinutes")]
    [InlineData("RateLimiting:Ip:PermitLimit", "0", "RateLimiting:Ip")]
    [InlineData("RateLimiting:WhatsAppWebhook:WindowSeconds", "86401", "RateLimiting:WhatsAppWebhook")]
    public void An_invalid_value_stops_the_start(string key, string value, string member)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(member, exception.Message, StringComparison.Ordinal);
    }
}
