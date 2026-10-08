using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppStartupTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void Availability_requires_number_and_both_webhook_secrets(bool number, bool token, bool app, bool verify)
    {
        using var provider = Provider(number, token, app, verify);
        provider.GetService<IStartupValidator>()?.Validate();
        var availability = provider.GetRequiredService<IWhatsAppAvailability>();
        Assert.Equal(number, availability.IsEnabled);
        Assert.Equal(number && app && verify, availability.IsWebhookEnabled);
        Assert.Equal(availability.IsWebhookEnabled, Assert.Single(provider.GetServices<IInstalledModuleRouteAvailability>()).IsAvailable);
    }
    [Theory]
    [InlineData(true, false, false, false, "WhatsApp:AccessToken")]
    [InlineData(true, true, true, false, "WhatsApp:VerifyToken")]
    [InlineData(true, true, false, true, "WhatsApp:AppSecret")]
    public void Missing_keys_fail_startup_without_revealing_values(bool number, bool token, bool app, bool verify, string expected)
    {
        using var provider = Provider(number, token, app, verify);
        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-secret", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public void Removed_module_has_no_installed_route_availability()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        Assert.Empty(provider.GetServices<IInstalledModuleRouteAvailability>());
    }
    [Fact]
    public void Defaults_follow_operations()
    {
        var options = new WhatsAppOptions();
        Assert.Equal("v25.0", options.GraphApiVersion); Assert.Equal(["AR"], options.AllowedCountries);
        Assert.Equal(100, options.DailyAuthCodeLimit);
        Assert.Equal("invitacion_organizacion", options.Templates.Invitation);
    }
    [Fact]
    public void Configured_country_array_replaces_the_default_instead_of_appending()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsApp:AllowedCountries:0"] = "US",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        WhatsAppInfrastructureModule.AddWhatsAppModule(services, configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(["US"], provider.GetRequiredService<IWhatsAppChannelSettings>().Countries);
    }

    private static ServiceProvider Provider(bool number, bool token, bool app, bool verify)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsApp:PhoneNumberId"] = number ? "fixture-number" : null,
            ["WhatsApp:AccessToken"] = token ? "fixture-secret" : null,
            ["WhatsApp:AppSecret"] = app ? "fixture-secret" : null,
            ["WhatsApp:VerifyToken"] = verify ? "fixture-secret" : null,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        WhatsAppInfrastructureModule.AddWhatsAppModule(services, configuration);
        WhatsAppApiModule.AddWhatsAppModule(services);
        return services.BuildServiceProvider();
    }
}
