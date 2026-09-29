using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class ProductionSeedTests
{
    [Fact]
    public async Task Empty_migrated_database_in_production_seeds_web_settings_and_legal_texts_once()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.3")
            .WithDatabase("postgres")
            .Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var ownerPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var runtimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var bootstrap = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Database = "postgres",
        }.ConnectionString;
        var admin = Connection("mt_owner", ownerPassword);
        var runtime = Connection("mt_app", runtimePassword);
        await DatabaseBootstrapExtensions.BootstrapAsync(bootstrap, admin, runtime,
            TestContext.Current.CancellationToken);

        var certificate = TestCertificate();
        using var missingOwnerHost = NewHost(string.Empty);
        var missingOwner = Assert.ThrowsAny<Exception>(() => missingOwnerHost.CreateClient());
        Assert.Contains("Seed:PlatformOwner:Email", missingOwner.ToString(), StringComparison.Ordinal);

        using var host = NewHost("platform-owner@example.test");
        using var client = host.CreateClient();

        // La primera llamada ocurre en Program al arrancar Production; repetir no agrega filas.
        await host.Services.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        using var existingOwnerHost = NewHost(string.Empty);
        using var existingOwnerClient = existingOwnerHost.CreateClient();

        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await context.PlatformSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConsumerSignupMode.Open, settings.ConsumerSignup);
        Assert.Equal(BusinessSignupMode.Open, settings.BusinessSignup);
        Assert.Equal(1, settings.MaxOwnedOrganizations);
        Assert.Equal(30, settings.AccountDeletionGraceDays);
        Assert.Single(await context.SecurityEvents.ToListAsync(TestContext.Current.CancellationToken));

        var operatorUser = await context.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(operatorUser.IsPlatformOperator);
        Assert.Equal("Operator Example", operatorUser.DisplayName);
        Assert.Equal("platform-owner@example.test", operatorUser.Email);
        Assert.False(await context.Tenants.AnyAsync(TestContext.Current.CancellationToken));
        var loginMethod = await context.LoginMethods.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LoginMethodType.Email, loginMethod.Type);
        Assert.True(loginMethod.IsPrimary);
        Assert.NotNull(loginMethod.VerifiedAtUtc);

        var documents = await context.LegalDocuments.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, documents.Count);
        Assert.All(documents, document => Assert.Equal(1, document.Version));
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Terms);
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Privacy);
        var contents = await context.LegalDocumentContents.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4, contents.Count);
        Assert.Contains(contents, content => content.Culture == "es-AR" &&
            content.Text == "DOCUMENTO DE DEMOSTRACIÓN. Esta plantilla no incluye términos legales. Reemplazá este texto antes de publicar.");
        Assert.Contains(contents, content => content.Culture == "en-US" &&
            content.Text == "DEMONSTRATION DOCUMENT. This template does not include a privacy policy. Replace this text before publishing.");

        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        Assert.NotNull(await applications.FindByClientIdAsync("web", TestContext.Current.CancellationToken));
        Assert.NotNull(await scopes.FindByNameAsync("api", TestContext.Current.CancellationToken));

        WebApplicationFactory<Program> NewHost(string ownerEmail) =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:appdb", runtime);
            builder.UseSetting("Authentication:Issuer", "https://example.test/");
            builder.UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://example.test/auth/callback");
            builder.UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://example.test/");
            builder.UseSetting("Authentication:Certificates:Encryption:Base64", certificate);
            builder.UseSetting("Authentication:Certificates:Signing:Base64", certificate);
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Seed:PlatformOwner:Email", ownerEmail);
            builder.UseSetting("Seed:PlatformOwner:DisplayName", "Operator Example");
        });

        string Connection(string user, string password) => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Database = "appdb",
            Username = user,
            Password = password,
        }.ConnectionString;
    }

    private static string TestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=production-seed-test", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));
        using var certificate = request.CreateSelfSigned(
            TimeProvider.System.GetUtcNow().AddMinutes(-1),
            TimeProvider.System.GetUtcNow().AddDays(1));
        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
