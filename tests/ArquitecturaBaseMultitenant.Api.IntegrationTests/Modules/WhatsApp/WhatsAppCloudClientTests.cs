using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppCloudClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Registered_client_overrides_global_retry_so_each_outbox_attempt_has_one_POST()
    {
        using var handler = new Handler(500, "{}");
        using var logs = new Logs();
        var services = new ServiceCollection(); services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler(options =>
        { options.Retry.MaxRetryAttempts = 1; options.Retry.Delay = TimeSpan.Zero; }));
        services.AddWhatsAppModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["WhatsApp:PhoneNumberId"] = "fixture-number", ["WhatsApp:AccessToken"] = "fixture-token" }).Build());
        services.AddSingleton(TimeProvider.System);
        // Desde la E11 el token sale del canal: el número conectado lo trae cifrado.
        var protector = new WhatsAppTokenProtector(Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("whatsapp-cloud-client-tests"));
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppTokenProtector>(protector);
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.IWhatsAppChannelReader>(new SendingReader(protector));
        services.AddHttpClient<WhatsAppCloudClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var receipt = await provider.GetRequiredService<WhatsAppCloudClient>().SendAsync(new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true), Ct);
        Assert.False(receipt.IsAccepted);
        Assert.True(receipt.IsRetryable);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(logs.Entries, value => value.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, value => value.Text.Contains("fixture-token", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(200, "{\"messages\":[{\"id\":\"wamid.fixture\"}]}", true, false, null)]
    [InlineData(200, "{}", true, false, null)]
    [InlineData(200, "[]", true, false, null)]
    [InlineData(200, "invalid-json", true, false, null)]
    [InlineData(400, "{\"error\":{\"code\":131026,\"message\":\"raw phone and code secret\"}}", false, false, "WhatsApp.Send.Undeliverable")]
    [InlineData(429, "{\"error\":{\"code\":130429}}", false, true, "WhatsApp.Send.RateLimited")]
    [InlineData(500, "{\"error\":{\"code\":1}}", false, true, "WhatsApp.Send.Rejected")]
    [InlineData(400, "invalid-json", false, false, "WhatsApp.Send.InvalidResponse")]
    [InlineData(500, "invalid-json", false, true, "WhatsApp.Send.InvalidResponse")]
    public async Task One_post_uses_explicit_number_and_safe_receipt(int status, string response, bool accepted, bool retryable, string? safeCode)
    {
        using var handler = new Handler(status, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var client = new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { PhoneNumberId = "default-not-used", AccessToken = "fixture-token" }), TimeProvider.System);
        var message = new WhatsAppOutboundMessage("explicit-number", "+5491134567890", "codigo_ingreso", "es", ["483920"], "483920", true);
        var receipt = await client.SendAsync(message, Ct);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("/v26.0/explicit-number/messages", handler.Path);
        Assert.Equal("Bearer fixture-token", handler.Authorization);
        Assert.Equal(accepted, receipt.IsAccepted);
        Assert.Equal(retryable, receipt.IsRetryable);
        Assert.Equal(safeCode, receipt.SafeCode);
        Assert.DoesNotContain("secret", receipt.ToString());
        Assert.DoesNotContain("483920", receipt.ToString());
        if (accepted) Assert.Equal("explicit-number", receipt.ProviderScope);
    }
    [Fact]
    public async Task Delayed_free_reply_expires_before_HTTP_while_templates_remain_sendable()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        using var handler = new Handler(200, "{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var client = new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), clock);
        var reply = new WhatsAppOutboundMessage("number", "+5491134567890", "", "es", [], null, false,
            Body: "Hola", ReplyExpiresAtUtc: clock.GetUtcNow().UtcDateTime.AddHours(24));
        clock.Advance(TimeSpan.FromHours(25));
        var expired = await client.SendAsync(reply, Ct);
        Assert.False(expired.IsAccepted); Assert.False(expired.IsRetryable);
        Assert.Equal("WhatsApp.Send.WindowExpired", expired.SafeCode); Assert.Equal(0, handler.Calls);
        var template = new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true);
        Assert.True((await client.SendAsync(template, Ct)).IsAccepted); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public void Argentine_provider_compatibility_never_changes_the_stored_phone()
    {
        var message = new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(WhatsAppCloudClient.BuildPayload(message, true)));
        Assert.Equal("541134567890", json.RootElement.GetProperty("to").GetString());
        Assert.Equal("+5491134567890", message.To);
    }
    private sealed class Handler(int status, string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Path = request.RequestUri!.AbsolutePath; Authorization = request.Headers.Authorization!.ToString();
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(response) });
        }
    }
    /// <summary>El número conectado con su token cifrado; las demás lecturas no se usan acá.</summary>
    private sealed class SendingReader(WhatsAppTokenProtector protector) : ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.IWhatsAppChannelReader
    {
        private readonly Guid id = Guid.NewGuid();
        public Task<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) =>
            Task.FromResult<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppSendingChannelRow?>(new(id, phoneNumberId, "waba", null,
                ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppNumberStatus.Connected, protector.Protect(id, "fixture-token")));
        public Task<ArquitecturaBaseMultitenant.Application.Common.Pagination.PagedResult<WhatsAppNumberResponse>> ListOrganizationAsync(WhatsAppNumberListRequest request, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppNumberResponse?> FindOrganizationAsync(Guid id, DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountOrganizationAsync(ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppNumberStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppNumberPendingRow>> ListPaymentMissingAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppPlatformNumberRow?> FindPlatformAsync(DateTime nowUtc, CancellationToken ct) => throw new NotSupportedException();
        public Task<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppPlatformChannelRow?> FindPlatformStateAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListOrganizationIdsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<WhatsAppInboundChannel?> FindInboundAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, string Text)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(this, categoryName);
        public void Dispose() { }
        private sealed class CapturedLogger(Logs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var text = formatter(state, exception);
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                    text += string.Join(";", fields.Select(value => value.Value?.ToString()));
                owner.Entries.Enqueue((category, text));
            }
        }
    }
}
