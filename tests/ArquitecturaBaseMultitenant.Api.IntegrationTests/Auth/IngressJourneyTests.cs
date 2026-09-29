using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class IngressJourneyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Signup_code_from_pickup_enters_a_personal_space_with_legal_acceptance()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var logs = new CapturedLogs();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var email = $"journey-{Guid.NewGuid():N}@example.test";

        using var requested = await PostOnceAsync(client, "/api/auth/signup",
            new { email, acceptedTerms = true, culture = "es-AR" });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var code = await ReadPickupCodeAsync(host.Services, email);
        using var verified = await PostOnceAsync(client, "/api/auth/signup/verify",
            new { email, code, acceptedTerms = true, culture = "es-AR" });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            value => value.Contains("Identity.Application", StringComparison.Ordinal));

        await AssertPersonalSignupAsync(email);
        Assert.True(!logs.Entries.Any(entry => entry.Contains(code, StringComparison.Ordinal)),
            "Un código de ingreso apareció en logs.");
    }

    private static async Task<HttpResponseMessage> PostOnceAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    private static async Task<string> ReadPickupCodeAsync(IServiceProvider provider, string email)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = Path.Combine(services.GetRequiredService<IHostEnvironment>().ContentRootPath, ".emails");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await services.GetRequiredService<IOutboxDispatchService>().DispatchOnceAsync(
                services.GetServices<IChannelSender>().ToArray(), Ct);
            foreach (var path in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.eml") : [])
            {
                string code;
                await using (var stream = File.OpenRead(path))
                {
                    using var message = await MimeMessage.LoadAsync(stream, Ct);
                    if (!message.To.Mailboxes.Any(mailbox => mailbox.Address == email)) continue;
                    var match = Regex.Match(message.TextBody ?? string.Empty, @"(?<!\d)\d{6}(?!\d)");
                    Assert.True(match.Success, "El correo pickup no contiene un código de seis dígitos.");
                    code = match.Value;
                }
                File.Delete(path);
                return code;
            }
            await Task.Delay(100, Ct);
        }
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup de registro.");
    }

    private async Task AssertPersonalSignupAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(DISTINCT a."TenantId"), count(DISTINCT l."Id")
            FROM identity."LoginMethods" m
            JOIN identity."UserTenantAccesses" a ON a."UserId" = m."UserId"
            JOIN platform."Tenants" t ON t."Id" = a."TenantId"
            JOIN identity."LegalAcceptances" l ON l."UserId" = m."UserId"
            WHERE m."Value" = @email AND m."VerifiedAtUtc" IS NOT NULL
              AND t."Kind" = 'Personal' AND t."Status" = 'Active'
            """;
        command.Parameters.AddWithValue("email", email);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(2, reader.GetInt64(1));
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IEnumerable<string> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(_entries);

        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(formatter(state, exception));
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose() { }
        }
    }
}
