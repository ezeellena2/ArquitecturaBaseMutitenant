using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba el seed de configuración, cliente web y documentos legales en una base vacía de producción.
/// Repetir el arranque no debe duplicar esos datos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class ProductionSeedTests
{
    [Theory]
    [InlineData(null, null, "Operator Example Name", "Operator", "Example Name")]
    [InlineData("María José", "de la Cruz", "Legacy Wrong", "María José", "de la Cruz")]
    [InlineData("Ana", null, "Legacy Wrong", "Ana", null)]
    [InlineData(null, "Pérez", "Legacy Wrong", null, "Pérez")]
    [InlineData("", "", "Legacy Wrong", null, null)]
    public void Owner_seed_prefers_explicit_parts_and_only_uses_legacy_when_neither_key_is_present(
        string? firstName, string? lastName, string legacy, string? expectedFirst, string? expectedLast)
    {
        var values = new Dictionary<string, string?>
        {
            ["Seed:PlatformOwner:Email"] = "owner@example.test",
            ["Seed:PlatformOwner:DisplayName"] = legacy,
        };
        if (firstName is not null) values["Seed:PlatformOwner:FirstName"] = firstName;
        if (lastName is not null) values["Seed:PlatformOwner:LastName"] = lastName;
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var seeder = new PlatformSeeder(null!, null!, null!, null!, configuration, null!, TimeProvider.System);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(seeder.ReadOwner());
        Assert.True(json.TryGetProperty("FirstName", out var first), "The seed must retain FirstName independently.");
        Assert.True(json.TryGetProperty("LastName", out var last), "The seed must retain LastName independently.");
        Assert.Equal(expectedFirst, first.GetString());
        Assert.Equal(expectedLast, last.GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_migrated_database_in_production_seeds_web_settings_and_legal_texts_once(bool separateNames)
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
        var expectedEvents = 2;
        ContributeSeedEventCount(ref expectedEvents);
        Assert.Equal(expectedEvents, initialEvents.Count);
        AssertModuleSeedEvents(initialEvents);
        Assert.Contains(initialEvents, item => item.Type == SecurityEventType.PlatformSettingsChanged);
        Assert.Contains(initialEvents, item => item.Type.ToString() == "PlatformOperatorGranted"
            && item.ActorKind == AuditActorKind.System && item.Reason == "Initial platform owner seed");

        var operatorUser = await context.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(operatorUser.IsPlatformOperator);
        var operatorPermissions = await context.PlatformPermissionAssignments.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(operatorUser.Id, operatorPermissions.UserId);
        Assert.Equal(ArquitecturaBaseMultitenant.Domain.Authorization.PlatformPermissions.All, operatorPermissions.Permissions);
        Assert.Equal(separateNames ? "Operator Middle Example Name" : "Operator Example", operatorUser.DisplayName);
        Assert.Equal(separateNames ? "Operator Middle" : "Operator", operatorUser.FirstName);
        Assert.Equal(separateNames ? "Example Name" : "Example", operatorUser.LastName);
        Assert.Equal("platform-owner@example.test", operatorUser.Email);
        Assert.False(await context.Tenants.AnyAsync(TestContext.Current.CancellationToken));
        var loginMethod = await context.LoginMethods.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LoginMethodType.Email, loginMethod.Type);
        Assert.True(loginMethod.IsPrimary);
        Assert.NotNull(loginMethod.VerifiedAtUtc);

        var documents = await context.LegalDocuments.ToListAsync(TestContext.Current.CancellationToken);
        var expectedVersions = new List<int> { 1 };
        ContributeLegalVersions(expectedVersions);
        Assert.Equal(2 * expectedVersions.Count, documents.Count);
        Assert.All(Enum.GetValues<LegalDocumentKind>(), kind =>
            Assert.Equal(expectedVersions, documents.Where(document => document.Kind == kind).Select(document => document.Version).Order().ToArray()));
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Terms);
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Privacy);
        var contents = await context.LegalDocumentContents.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(4 * expectedVersions.Count, contents.Count);
        AssertModuleLegalContents(documents, contents);
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
        Assert.Equal(1, await context.PlatformPermissionAssignments.AsNoTracking().CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await context.Users.AsNoTracking().AnyAsync(user => user.IsPlatformOperator,
            TestContext.Current.CancellationToken));
        Assert.Equal(2, (await context.SecurityEvents.AsNoTracking()
            .ToArrayAsync(TestContext.Current.CancellationToken))
            .Count(item => item.Type.ToString() == "PlatformOperatorGranted"));

        // Reparar un indicador legado sin asignación no otorga permisos nuevos de módulos opcionales.
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM platform."PlatformPermissionAssignments" WHERE "UserId" = {operatorUser.Id}
            """, TestContext.Current.CancellationToken);
        using (var legacyHost = NewHost("pending-owner@example.test"))
        using (var legacyClient = legacyHost.CreateClient())
        {
            var repaired = await context.PlatformPermissionAssignments.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(ArquitecturaBaseMultitenant.Domain.Authorization.PlatformPermissions.LegacyBootstrap, repaired.Permissions);
            foreach (var permission in ArquitecturaBaseMultitenant.Domain.Authorization.PlatformPermissions.All
                .Except(ArquitecturaBaseMultitenant.Domain.Authorization.PlatformPermissions.LegacyBootstrap, StringComparer.Ordinal))
                Assert.DoesNotContain(permission, repaired.Permissions);
        }

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
            builder.UseSetting("Email:Delivery", "Smtp");
            builder.UseSetting("Email:Smtp:UserName", "sender@example.test");
            builder.UseSetting("Email:Smtp:Password", "test-only-password");
            builder.UseSetting("Email:Smtp:FromAddress", "sender@example.test");
            builder.UseSetting("Seed:PlatformOwner:Email", ownerEmail);
            builder.UseSetting("Seed:PlatformOwner:DisplayName", "Operator Example");
            if (separateNames)
            {
                builder.UseSetting("Seed:PlatformOwner:FirstName", "Operator Middle");
                builder.UseSetting("Seed:PlatformOwner:LastName", "Example Name");
            }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailTransport>();
                services.AddScoped<IEmailTransport, NoopEmailTransport>();
            });
        });

        string Connection(string user, string password) => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Database = "appdb",
            Username = user,
            Password = password,
        }.ConnectionString;
    }

    static partial void ContributeSeedEventCount(ref int count);
    static partial void AssertModuleSeedEvents(List<SecurityEvent> events);
    static partial void ContributeLegalVersions(List<int> versions);
    static partial void AssertModuleLegalContents(List<LegalDocument> documents, List<LegalDocumentContent> contents);

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

    private sealed class NoopEmailTransport : IEmailTransport
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
