using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Binding privado y removible del módulo para el recorrido combinado.</summary>
public static class RecorridoWhatsAppModule
{
    public static void Configure(IWebHostBuilder builder) => builder
        // Program compone la disponibilidad al registrar DI, antes de los callbacks de configuración de WAF.
        .UseSetting("WhatsApp:PhoneNumberId", WhatsAppBrowserControl.NumberId)
        .UseSetting("WhatsApp:AccessToken", "browser-fixture-token")
        .UseSetting("WhatsApp:AppSecret", WhatsAppBrowserControl.AppSecret)
        .UseSetting("WhatsApp:VerifyToken", "browser-fixture-verify")
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:PlatformOwner:Phone"] = WhatsAppBrowserControl.OperatorPhone,
            ["WhatsApp:PhoneNumberId"] = WhatsAppBrowserControl.NumberId,
            ["WhatsApp:AccessToken"] = "browser-fixture-token",
            ["WhatsApp:AppSecret"] = WhatsAppBrowserControl.AppSecret,
            ["WhatsApp:VerifyToken"] = "browser-fixture-verify",
            ["WhatsApp:DisplayPhoneNumber"] = "+5491134567890",
            ["WhatsApp:ProcessInboundInBackground"] = "false",
            ["WhatsApp:DailyAuthCodeLimit"] = "1000",
            ["WhatsApp:ProtectedAuthCodeFraction"] = "0.2",
            ["WhatsApp:DailyNewAuthCodeIpLimit"] = "1000",
        }))
        .ConfigureTestServices(services =>
        {
            var senders = services.Where(descriptor => descriptor.ServiceType == typeof(IChannelSender)
                && descriptor.ImplementationType?.FullName?.Contains(".Modules.WhatsApp.", StringComparison.Ordinal) == true).ToArray();
            foreach (var descriptor in senders) services.Remove(descriptor);
            services.AddSingleton<IChannelSender, ForbiddenWhatsAppSender>();
        });

    private sealed class ForbiddenWhatsAppSender : IChannelSender
    {
        public string Key => "whatsapp";
        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("WhatsApp transport is forbidden in the isolated walkthrough.");
    }
}
