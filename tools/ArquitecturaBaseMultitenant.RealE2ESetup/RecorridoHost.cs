using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Una API Testing real, preparación E7 y control E8 sobre el PostgreSQL efímero de Aspire.</summary>
internal static class RecorridoHost
{
    public static async Task<int> RunAsync(Email email, string readyFile, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("MT_E2E_ISOLATED") != "1")
            throw new InvalidOperationException("Unified host requires isolated mode.");
        RecorridoPaths.Validate(readyFile);
        var pickup = Required("MT_E2E_PICKUP_DIR");
        if (!string.Equals(Path.GetFullPath(pickup), Path.GetDirectoryName(Path.GetFullPath(readyFile)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Pickup and control must share the owned temporary workspace.");
        var connections = new IsolatedDatabaseConnections(Required("ConnectionStrings__postgres-bootstrap"),
            Required("ConnectionStrings__appdb-admin"), Required("ConnectionStrings__appdb"));
        var port = int.Parse(Required("MT_E2E_API_PORT"), CultureInfo.InvariantCulture);
        if (port is <= 0 or > 65535) throw new InvalidOperationException("Invalid isolated API port.");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ArquitecturaBaseMultitenant.slnx"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("Repository root missing.");

        await using var factory = ApiFactory.ForIsolatedDatabase(connections);
        await factory.InitializeDatabaseAsync(ct);
        await using var app = RecorridoApiHost.Configure(factory, repository.FullName, pickup, builder =>
        {
            RecorridoWhatsAppModule.Configure(builder);
            RecorridoAssistantModule.Configure(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPublicOrigin>();
                services.AddSingleton<IPublicOrigin, E2EPublicOrigin>();
            });
        });
        // Certificado efímero propio. El cliente interno fija su huella; Vite conserva HTTPS5174.
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        var now = TimeProvider.System.GetUtcNow();
        using var generated = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        app.UseKestrel(options => options.Listen(IPAddress.Loopback, port, listen => listen.UseHttps(certificate)));
        using var startup = app.CreateClient();
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, peer, _, _) => peer?.GetCertHashString() == certificate.GetCertHashString(),
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{port}/") };
        await app.Services.SeedDatabaseAsync(ct);
        var prepared = await E7Preparation.PrepareAsync(app.Services, email, ct);
        var bridge = new WhatsAppFileBridge(app.Services, readyFile, isolated: true);
        var assistant = new AssistantFileBridge(app.Services, readyFile, isolated: true);
        await using (var marker = new FileStream(readyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await marker.FlushAsync(ct);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await LegalVersionCommand.TryExecuteAsync(app.Services, readyFile, ct);
            await InvitationCommand.TryExecuteAsync(app.Services, readyFile, prepared.BusinessId, prepared.UserId, ct);
            await PlatformCommand.TryExecuteAsync(app.Services, readyFile, prepared.BusinessId, prepared.UserId, ct);
            await LegacyBrowserSessionCommand.TryExecuteAsync(app.Services, readyFile, ct);
            await LoginCodeCommand.TryExecuteAsync(app.Services, readyFile, ct);
            await bridge.TryExecuteAsync(client, ct);
            await assistant.TryExecuteAsync(ct);
            // Un único claim email por tick; WhatsApp cifrado permanece disponible para su control privado.
            await RecorridoEmailPump.ProcessOnceAsync(app.Services, ct);
        }
        return 0;
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name)
        is { Length: > 0 } value ? value : throw new InvalidOperationException("Required isolated host setting missing.");
}
