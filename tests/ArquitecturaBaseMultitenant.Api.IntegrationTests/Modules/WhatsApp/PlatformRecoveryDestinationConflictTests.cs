using System.Net;
using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

public sealed partial class PlatformRecoveryDestinationConflictTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task Approval_rejects_a_phone_acquired_after_submission(bool own) =>
        AssertConflictAsync(LoginMethodType.Phone, own, PhoneHost, SubmitPhoneAsync);

    private static WebApplicationFactory<Program> PhoneHost(ApiFactory factory) => factory.WithWebHostBuilder(builder => builder
        .UseEnvironment("Testing")
        .UseSetting("WhatsApp:PhoneNumberId", "recovery-conflict-number")
        .UseSetting("WhatsApp:AccessToken", "fixture-token")
        .UseSetting("WhatsApp:AppSecret", "fixture-secret")
        .UseSetting("WhatsApp:VerifyToken", "fixture-verify")
        .UseSetting("WhatsApp:AllowedCountries:0", "AR")
        .ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            foreach (var sender in services.Where(descriptor => descriptor.ServiceType == typeof(IChannelSender)
                && descriptor.ImplementationType?.FullName?.Contains(".Modules.WhatsApp.", StringComparison.Ordinal) == true).ToArray())
                services.Remove(sender);
            services.AddSingleton<IChannelSender, ForbiddenWhatsAppSender>();
        }));

    private static async Task SubmitPhoneAsync(WebApplicationFactory<Program> app, HttpClient client, string destination,
        string previousEmail)
    {
        var nonce = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        object Body(string? code = null, bool submit = false) => new { phone = new { country = "AR", number = destination }, channel = "whatsapp", nonce, code, previousEmail = submit ? previousEmail : null };
        using var requested = await AccountJourney.PostAsync(client, "/api/account-recovery/code", Body(), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await WhatsAppIdentityTests.CodeAsync(app.Services, destination);
        using var verified = await AccountJourney.PostAsync(client, "/api/account-recovery/verify", Body(code), Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(client, "/api/account-recovery", Body(submit: true), Ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
    }

    private sealed class ForbiddenWhatsAppSender : IChannelSender
    {
        public string Key => "whatsapp";
        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("WhatsApp transport is forbidden in recovery conflict tests.");
    }

}
