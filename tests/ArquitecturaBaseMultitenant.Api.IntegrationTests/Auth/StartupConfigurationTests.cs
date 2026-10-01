using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba la validación de Google y correo al arrancar. Exige credenciales cuando corresponden y rechaza
/// transportes de prueba en producción sin imprimir secretos.
/// </summary>
public sealed class StartupConfigurationTests
{
    [Fact]
    public void Development_settings_enable_Google_and_Gmail_without_committing_credentials()
    {
        var apiDirectory = Path.Combine(FindRepositoryRoot(), "src", "ArquitecturaBaseMultitenant.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        Assert.False(string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]));
        Assert.Equal("Smtp", configuration["Email:Delivery"]);
        Assert.Equal("smtp.gmail.com", configuration["Email:Smtp:Host"]);
        Assert.Equal("587", configuration["Email:Smtp:Port"]);
        Assert.False(string.IsNullOrWhiteSpace(configuration["Email:Smtp:UserName"]));
        Assert.False(string.IsNullOrWhiteSpace(configuration["Email:Smtp:FromAddress"]));

        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(apiDirectory, file)))!;
            Assert.Null(node["Authentication"]?["Google"]?["ClientSecret"]);
            Assert.Null(node["Authentication"]?["LoginCode"]?["HashKey"]);
            Assert.Null(node["Email"]?["Smtp"]?["Password"]);
        }
    }

    [Fact]
    public void Smtp_without_password_fails_at_startup_and_names_only_the_key()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Delivery"] = "Smtp",
            ["Email:Smtp:Host"] = "smtp.example.test",
            ["Email:Smtp:Port"] = "587",
            ["Email:Smtp:UserName"] = "sender@example.test",
            ["Email:Smtp:FromName"] = "Test",
            ["Email:Smtp:FromAddress"] = "sender@example.test",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new TestHostEnvironment();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddEmail(configuration, environment);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Email:Smtp:Password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pickup_delivery_is_rejected_in_production()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Delivery"] = "PickupDirectory",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEmail(configuration, new TestHostEnvironment { EnvironmentName = "Production" });
        using var provider = services.BuildServiceProvider();

        var exception = Assert.ThrowsAny<Exception>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Email:Delivery", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Google_client_id_without_secret_fails_at_startup_and_names_only_the_key()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Google:ClientId"] = "public-test-client",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>();
        services.AddIdentityServices(configuration, new TestHostEnvironment());
        using var provider = services.BuildServiceProvider();

        var exception = Assert.ThrowsAny<Exception>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains("Authentication:Google:ClientSecret", exception.Message, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
