using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba que también se prepare el mensaje para una cuenta desconocida. Protege el comportamiento que
/// evita revelar la existencia de un correo.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeEnumerationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_account_still_renders_the_login_message()
    {
        var renderer = new RecordingRenderer();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailTemplateRenderer>();
            services.AddSingleton<IEmailTemplateRenderer>(renderer);
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-code")
        {
            Content = JsonContent.Create(new { email = $"unknown-{Guid.NewGuid():N}@example.test" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, renderer.LoginRenders);
    }

    private sealed class RecordingRenderer : IEmailTemplateRenderer
    {
        public int LoginRenders { get; private set; }

        public EmailMessage RenderLoginCode(string to, string code, int lifetimeMinutes, CultureProfile culture)
        {
            LoginRenders++;
            return new EmailMessage(to, "Test", "Test", "Test");
        }

        public EmailMessage RenderSignupCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
            throw new NotSupportedException();

        public EmailMessage RenderVerifyEmailCode(string to, string code, int lifetimeMinutes, CultureProfile culture) =>
            throw new NotSupportedException();

        public EmailMessage RenderInvitation(string to, string loginUrl, CultureProfile culture) =>
            throw new NotSupportedException();

        public Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice,
            CultureProfile culture, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
