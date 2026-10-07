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
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba el error de campo para una cuenta desconocida sin preparar, emitir ni enviar un código (ADR 0043).
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeEnumerationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unknown_account_returns_a_field_error_without_rendering_issuing_or_sending_a_code()
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
        var email = $"unknown-{Guid.NewGuid():N}@example.test";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxBefore = await db.OutboxMessages.CountAsync(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-code")
        {
            Content = JsonContent.Create(new { email }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Auth.LoginCode.AccountNotFound", problem.GetProperty("code").GetString());
        Assert.Equal("No hay una cuenta con ese correo.", problem.GetProperty("errors").GetProperty("email")[0].GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.Equal(0, renderer.LoginRenders);
        Assert.False(await db.LoginCodes.AnyAsync(code => code.Destination == email, Ct));
        Assert.Equal(outboxBefore, await db.OutboxMessages.CountAsync(Ct));
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

        public Task<EmailMessage> RenderInvitationAsync(string to,
            ArquitecturaBaseMultitenant.Application.Models.Invitations.InvitationNotice notice,
            CultureProfile culture, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EmailMessage> RenderAccountNoticeAsync(string to, AccountNotice notice,
            CultureProfile culture, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
