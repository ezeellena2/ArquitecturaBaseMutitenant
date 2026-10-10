using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Kestrel y PostgreSQL exclusivos para el navegador real; ningún control se expone por HTTP.</summary>
public static class WhatsAppBrowserFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static async Task<int> RunAsync(string root, string script, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(script) || !File.Exists(script) || Path.GetExtension(script) != ".mjs")
            throw new ArgumentException("Fixture runner must be an existing absolute .mjs path.", nameof(script));
        var front = Path.GetFullPath(Path.Combine(root, "../ArquitecturaBaseMutitenantFront"));
        var files = Path.Combine(Path.GetTempPath(), "mt-e8-browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(files);
        var certFile = Path.Combine(files, "certificate.pem");
        var keyFile = Path.Combine(files, "key.pem");
        var now = TimeProvider.System.GetUtcNow();
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localtest.me", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localtest.me"); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        // Importar PFX deja la clave privada disponible para Schannel en Windows.
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        try
        {
            await File.WriteAllTextAsync(certFile, certificate.ExportCertificatePem(), ct);
            await File.WriteAllTextAsync(keyFile, key.ExportPkcs8PrivateKeyPem(), ct);
            await using var factory = new ApiFactory();
            await factory.InitializeAsync();
            await using var app = factory.WithWebHostBuilder(builder => builder
                .UseContentRoot(Path.Combine(root, "src/ArquitecturaBaseMultitenant.Api"))
                .UseSetting("Authentication:Issuer", "https://localtest.me:5176/")
                .UseSetting("Authentication:Clients:Web:RedirectUris:0", "https://localtest.me:5176/auth/callback")
                .UseSetting("Authentication:Clients:Web:RedirectUris:1", "https://localtest.me:5176/silent-renew.html")
                .UseSetting("Authentication:Clients:Web:PostLogoutRedirectUris:0", "https://localtest.me:5176/")
                .UseSetting("Seed:PlatformOwner:Email", WhatsAppBrowserControl.OperatorEmail)
                .UseSetting("Seed:PlatformOwner:FirstName", "Operador")
                .UseSetting("Seed:PlatformOwner:LastName", "E2E")
                .UseSetting("Seed:PlatformOwner:Phone", WhatsAppBrowserControl.OperatorPhone)
                .UseSetting("WhatsApp:PhoneNumberId", WhatsAppBrowserControl.NumberId)
                .UseSetting("WhatsApp:AccessToken", "browser-fixture-token")
                .UseSetting("WhatsApp:AppSecret", WhatsAppBrowserControl.AppSecret)
                .UseSetting("WhatsApp:VerifyToken", "browser-fixture-verify")
                .UseSetting("WhatsApp:DisplayPhoneNumber", "+5491134567890")
                .UseSetting("WhatsApp:ProcessInboundInBackground", "false")
                // Perfil UI: 38 grupos, margen de 20 OTP/grupo (760 nuevos); defaults de cuota se prueban aparte.
                .UseSetting("WhatsApp:DailyAuthCodeLimit", "1000")
                .UseSetting("WhatsApp:ProtectedAuthCodeFraction", "0.2")
                .UseSetting("WhatsApp:DailyNewAuthCodeIpLimit", "1000")
                .UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0")
                .UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "100")
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureTestServices(services =>
                {
                    // Además de no despachar en Testing, reemplaza todos los sender: una llamada accidental nunca llega a Meta/SMTP.
                    services.RemoveAll<IChannelSender>();
                    services.AddSingleton<IChannelSender>(new ForbiddenSender("whatsapp"));
                    services.AddSingleton<IChannelSender>(new ForbiddenSender("email"));
                }));
            app.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
            using var startupClient = app.CreateClient();
            var endpoint = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, peer, _, _) => peer?.GetCertHashString() == certificate.GetCertHashString(),
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(endpoint) };
            await app.Services.SeedDatabaseAsync(ct);
            var control = new WhatsAppBrowserControl(app.Services);
            var psi = new ProcessStartInfo("node")
            {
                WorkingDirectory = front, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(script);
            psi.Environment["API_HTTPS"] = endpoint;
            psi.Environment["services__api__https__0"] = psi.Environment["API_HTTPS"];
            psi.Environment["VITE_AUTH_ISSUER"] = "https://localtest.me:5176/";
            psi.Environment["MT_E2E_ISOLATED"] = "1";
            psi.Environment["E8_CERT"] = certFile; psi.Environment["E8_KEY"] = keyFile;
            using var browser = Process.Start(psi) ?? throw new InvalidOperationException("Fixture runner did not start.");
            // Consumir stderr sin publicarlo: puede contener una URL o una prueba secreta del navegador.
            var errors = browser.StandardError.ReadToEndAsync(ct);
            try
            {
                while (await browser.StandardOutput.ReadLineAsync(ct) is { } line)
                {
                    if (line.StartsWith("CHECK ", StringComparison.Ordinal)) Console.WriteLine(line);
                    if (!line.StartsWith("FIXTURE ", StringComparison.Ordinal)) continue;
                    if (line.Length > 2048) throw new InvalidOperationException("Fixture request too large.");
                    var command = JsonSerializer.Deserialize<WhatsAppBrowserRequest>(line[8..], JsonOptions)
                        ?? throw new InvalidOperationException("Fixture request missing.");
                    var reply = await control.ExecuteAsync(command, client, ct);
                    await browser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(reply));
                    await browser.StandardInput.FlushAsync(ct);
                }
                await browser.WaitForExitAsync(ct);
                await errors;
                if (browser.ExitCode != 0) Console.Error.WriteLine("Fixture browser failed; inspect the runner's sanitized report.");
                return browser.ExitCode;
            }
            finally
            {
                if (!browser.HasExited) { browser.Kill(entireProcessTree: true); await browser.WaitForExitAsync(CancellationToken.None); }
            }
        }
        finally { File.Delete(certFile); File.Delete(keyFile); Directory.Delete(files); }
    }

    private sealed class ForbiddenSender(string key) : IChannelSender
    {
        public string Key => key;
        public Task SendAsync(string payload, CancellationToken cancellationToken) => throw new InvalidOperationException("External transport is forbidden in this fixture.");
    }
}
