using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Infrastructure;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    [Fact]
    public async Task OpenApi_exporter_uses_ephemeral_data_protection_without_database()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>();
        services.AddIdentityServices(new ConfigurationBuilder().Build(), new TestEnvironment("Testing"),
            isOpenApiExporter: true);
        await using var provider = services.BuildServiceProvider();

        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("openapi-export");
        var protectedValue = protector.Protect("schema");

        Assert.Equal("schema", protector.Unprotect(protectedValue));
        Assert.Null(provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
    }

    [Fact]
    public void Production_requires_data_protection_key_encryption()
    {
        var services = new ServiceCollection();
        var certificate = TestCertificate();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Authentication:Certificates:Encryption:Base64"] = certificate,
                ["Authentication:Certificates:Signing:Base64"] = certificate,
            }).Build();

        var failure = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, new TestEnvironment("Production")));

        Assert.Contains("DataProtection:Certificate:Base64", failure.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>();
        services.AddIdentityServices(configuration, new TestEnvironment("Testing"));
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static string TestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=data-protection-test", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            TimeProvider.System.GetUtcNow().AddMinutes(-1),
            TimeProvider.System.GetUtcNow().AddDays(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
