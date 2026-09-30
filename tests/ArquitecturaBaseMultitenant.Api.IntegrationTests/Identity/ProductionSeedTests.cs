using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
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
        host.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("production-key-encryption-test").Protect("probe");
        var keyXml = await context.DataProtectionKeys.AsNoTracking()
            .Select(key => key.Xml).ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(keyXml);
        Assert.All(keyXml, xml =>
        {
            Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("<masterKey", xml, StringComparison.Ordinal);
        });
        var settings = await context.PlatformSettings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConsumerSignupMode.Open, settings.ConsumerSignup);
        Assert.Equal(BusinessSignupMode.Open, settings.BusinessSignup);
        Assert.Equal(1, settings.MaxOwnedOrganizations);
        Assert.Equal(30, settings.AccountDeletionGraceDays);
        Assert.True(await context.Set<Culture>().AnyAsync(culture => culture.IsEnabled,
            TestContext.Current.CancellationToken));
        Assert.True(await context.Set<Currency>().AnyAsync(currency => currency.IsEnabled,
            TestContext.Current.CancellationToken));
        var initialEvents = await context.SecurityEvents.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, initialEvents.Count);
        Assert.Contains(initialEvents, item => item.Type == SecurityEventType.PlatformSettingsChanged);
        Assert.Contains(initialEvents, item => item.Type.ToString() == "PlatformOperatorGranted"
            && item.ActorKind == AuditActorKind.System && item.Reason == "Initial platform owner seed");

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

        // Una instalación existente puede tener el correo configurado sin haber probado su posesión.
        var pendingMethodId = Guid.CreateVersion7();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE identity."AspNetUsers" SET "IsPlatformOperator" = false WHERE "Id" = {operatorUser.Id}
            """, TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."LoginMethods"
                ("Id", "UserId", "Type", "Value", "IsPrimary", "VerifiedAtUtc")
            VALUES ({pendingMethodId}, {operatorUser.Id}, 'Email', 'pending-owner@example.test', false, NULL)
            """, TestContext.Current.CancellationToken);
        using (var unverifiedHost = NewHost("pending-owner@example.test"))
        {
            var rejected = Assert.ThrowsAny<Exception>(() => unverifiedHost.CreateClient());
            Assert.Contains("Seed:PlatformOwner:Email", rejected.ToString(), StringComparison.Ordinal);
        }
        Assert.False(await context.Users.AsNoTracking().AnyAsync(user => user.IsPlatformOperator,
            TestContext.Current.CancellationToken));

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE identity."LoginMethods"
            SET "VerifiedAtUtc" = {TimeProvider.System.GetUtcNow().UtcDateTime}, "ManagedByTenantId" = {Guid.CreateVersion7()}
            WHERE "Id" = {pendingMethodId}
            """, TestContext.Current.CancellationToken);
        using (var managedHost = NewHost("pending-owner@example.test"))
        {
            var rejected = Assert.ThrowsAny<Exception>(() => managedHost.CreateClient());
            Assert.Contains("Seed:PlatformOwner:Email", rejected.ToString(), StringComparison.Ordinal);
        }
        Assert.False(await context.Users.AsNoTracking().AnyAsync(user => user.IsPlatformOperator,
            TestContext.Current.CancellationToken));

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE identity."LoginMethods" SET "ManagedByTenantId" = NULL
            WHERE "Id" = {pendingMethodId}
            """, TestContext.Current.CancellationToken);
        using var promotedHost = NewHost("pending-owner@example.test");
        using var promotedClient = promotedHost.CreateClient();
        Assert.True(await context.Users.AsNoTracking().AnyAsync(user => user.IsPlatformOperator,
            TestContext.Current.CancellationToken));
        Assert.Equal(2, (await context.SecurityEvents.AsNoTracking()
            .ToArrayAsync(TestContext.Current.CancellationToken))
            .Count(item => item.Type.ToString() == "PlatformOperatorGranted"));

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
            builder.UseSetting("DataProtection:Certificate:Base64", certificate);
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
