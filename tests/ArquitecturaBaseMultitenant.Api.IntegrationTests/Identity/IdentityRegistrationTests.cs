using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class IdentityRegistrationTests(ApiFactory factory)
{
    [Fact]
    public async Task Application_uses_its_single_identity_context_and_persistent_cookie_settings()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<UserManager<ApplicationUser>>());
        Assert.NotNull(services.GetRequiredService<SignInManager<ApplicationUser>>());
        var store = services.GetRequiredService<IUserStore<ApplicationUser>>();
        Assert.Contains(typeof(ApplicationDbContext), store.GetType().GetGenericArguments());
        Assert.Same(services.GetRequiredService<ApplicationDbContext>(),
            services.GetRequiredService<IServiceProvider>().GetRequiredService<ApplicationDbContext>());

        var authentication = services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, authentication.DefaultScheme);
        var cookie = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromDays(30), cookie.ExpireTimeSpan);
        Assert.Equal(TimeSpan.Zero,
            services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
        Assert.StartsWith("EntityFrameworkCoreXmlRepository",
            services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository?.GetType().Name);
    }

    [Fact]
    public async Task Google_is_only_registered_when_client_id_is_configured()
    {
        var absent = BuildProvider(new ConfigurationBuilder().Build());
        await using (absent)
        {
            Assert.False(absent.GetRequiredService<IGoogleAvailability>().IsEnabled);
            Assert.Null(await absent.GetRequiredService<IAuthenticationSchemeProvider>()
                .GetSchemeAsync(GoogleDefaults.AuthenticationScheme));
        }

        var configured = BuildProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "public-test-client",
                ["Authentication:Google:ClientSecret"] = "test-only-secret",
            }).Build());
        await using (configured)
        {
            Assert.True(configured.GetRequiredService<IGoogleAvailability>().IsEnabled);
            Assert.NotNull(await configured.GetRequiredService<IAuthenticationSchemeProvider>()
                .GetSchemeAsync(GoogleDefaults.AuthenticationScheme));
        }
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>();
        services.AddIdentityServices(configuration);
        return services.BuildServiceProvider();
    }
}
