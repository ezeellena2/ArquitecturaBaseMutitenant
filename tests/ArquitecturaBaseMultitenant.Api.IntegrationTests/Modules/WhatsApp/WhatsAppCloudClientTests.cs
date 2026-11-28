using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
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
        // Un 5xx sin código no dice si Meta lo procesó: queda «Sin confirmar» y nadie lo repite (ADR 0066).
        Assert.Equal(OutboxAttemptOutcome.Uncertain, receipt.Outcome);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(logs.Entries, value => value.Category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Entries, value => value.Text.Contains("fixture-token", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Registered_client_confirms_a_twelve_second_response_instead_of_leaving_it_unconfirmed()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
        using var handler = new DelayedHandler(clock, TimeSpan.FromSeconds(12), 200, "{\"messages\":[{\"id\":\"wamid.slow\"}]}");
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddLogging();
        services.AddWhatsAppModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["WhatsApp:PhoneNumberId"] = "fixture-number", ["WhatsApp:AccessToken"] = "fixture-token" }).Build());
        var protector = new WhatsAppTokenProtector(Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("whatsapp-cloud-client-slow-tests"));
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppTokenProtector>(protector);
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.IWhatsAppChannelReader>(new SendingReader(protector));
        services.AddHttpClient<WhatsAppCloudClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WhatsAppCloudClient>();
        var message = new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true);

        var send = client.SendAsync(message, Ct);
        await handler.Entered.Task.WaitAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(12));
        var confirmed = await send;

        Assert.Equal((OutboxAttemptOutcome.Accepted, "wamid.slow", 1), (confirmed.Outcome, confirmed.ProviderMessageId, handler.Calls));

        // Pasada la espera del canal (15 s), la resiliencia corta y deja el mensaje «Sin confirmar».
        handler.Reset(TimeSpan.FromSeconds(16));
        var slow = client.SendAsync(message, Ct);
        await handler.Entered.Task.WaitAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(15));
        var timedOut = await slow;

        Assert.Equal((OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode), (timedOut.Outcome, timedOut.SafeCode));
    }

    [Fact]
    public async Task Many_5xx_failures_on_one_number_do_not_break_sends_for_another()
    {
        using var handler = new RoutingHandler(request => request.RequestUri!.AbsolutePath.Contains("/failing-number/", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.healthy\"}]}") });
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddWhatsAppModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["WhatsApp:PhoneNumberId"] = "fixture-number", ["WhatsApp:AccessToken"] = "fixture-token" }).Build());
        var protector = new WhatsAppTokenProtector(Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("whatsapp-cloud-client-breaker-tests"));
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations.IWhatsAppTokenProtector>(protector);
        services.AddSingleton<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.IWhatsAppChannelReader>(new SendingReader(protector));
        services.AddHttpClient<WhatsAppCloudClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<WhatsAppCloudClient>();

        for (var index = 0; index < 120; index++)
        {
            var failed = await client.SendAsync(new WhatsAppOutboundMessage("failing-number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true), Ct);
            Assert.Equal(OutboxAttemptOutcome.Uncertain, failed.Outcome);
        }

        var healthy = await client.SendAsync(new WhatsAppOutboundMessage("healthy-number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true), Ct);
        Assert.Equal((OutboxAttemptOutcome.Accepted, "wamid.healthy", 121), (healthy.Outcome, healthy.ProviderMessageId, handler.Calls));
    }
    /// <summary>
    /// La tabla de respuestas de Meta (ADR 0066): lo que no salió vuelve a la cola, lo definitivo falla con uno de los ocho
    /// motivos (o el medio de pago) y lo que no se sabe queda «Sin confirmar». Solo se lee el código de Meta.
    /// </summary>
    [Theory]
    [InlineData(200, "{\"messages\":[{\"id\":\"wamid.fixture\"}]}", OutboxAttemptOutcome.Accepted, null)]
    [InlineData(200, "{}", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(200, "[]", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(200, "{\"messages\":[{\"id\":\"\"}]}", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(200, "invalid-json", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(400, "{\"error\":{\"code\":131030}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.NotAllowed)]
    [InlineData(400, "{\"error\":{\"code\":131047}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.WindowClosed)]
    [InlineData(400, "{\"error\":{\"code\":131026,\"message\":\"raw phone and code secret\"}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Undeliverable)]
    [InlineData(400, "{\"error\":{\"code\":131042}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.PaymentRequired)]
    [InlineData(400, "{\"error\":{\"code\":131049}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.RateLimited)]
    [InlineData(429, "{\"error\":{\"code\":130429}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.RateLimited)]
    [InlineData(400, "{\"error\":{\"code\":131056}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.RateLimited)]
    [InlineData(400, "{\"error\":{\"code\":80007}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.RateLimited)]
    [InlineData(503, "{\"error\":{\"code\":131016}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData(500, "{\"error\":{\"code\":131000}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData(500, "{\"error\":{\"code\":2}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData(503, "{\"error\":{\"code\":133004}}", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData(400, "{\"error\":{\"code\":131000}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Rejected)]
    [InlineData(401, "{\"error\":{\"code\":190}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.TokenInvalid)]
    [InlineData(400, "{\"error\":{\"code\":0}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.TokenInvalid)]
    [InlineData(403, "{\"error\":{\"code\":10}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.PermissionMissing)]
    [InlineData(500, "{\"error\":{\"code\":3}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.PermissionMissing)]
    [InlineData(400, "{\"error\":{\"code\":200}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.PermissionMissing)]
    [InlineData(400, "{\"error\":{\"code\":100}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Rejected)]
    [InlineData(500, "{\"error\":{\"code\":1}}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Rejected)]
    [InlineData(401, "invalid-json", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.TokenInvalid)]
    [InlineData(403, "{}", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.PermissionMissing)]
    [InlineData(429, "invalid-json", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.RateLimited)]
    [InlineData(400, "invalid-json", OutboxAttemptOutcome.Rejected, WhatsAppSendFailures.Rejected)]
    [InlineData(500, "invalid-json", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(502, "{}", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData(500, "{\"error\":{\"message\":\"no code\"}}", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    public async Task One_post_uses_explicit_number_and_safe_receipt(int status, string response, OutboxAttemptOutcome outcome, string? safeCode)
    {
        using var handler = new Handler(status, response);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var client = new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { PhoneNumberId = "default-not-used", AccessToken = "fixture-token" }), TimeProvider.System);
        var message = new WhatsAppOutboundMessage("explicit-number", "+5491134567890", "codigo_ingreso", "es", ["483920"], "483920", true);
        var receipt = await client.SendAsync(message, Ct);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("/v26.0/explicit-number/messages", handler.Path);
        Assert.Equal("Bearer fixture-token", handler.Authorization);
        Assert.Equal(outcome, receipt.Outcome);
        Assert.Equal(safeCode, receipt.SafeCode);
        Assert.DoesNotContain("secret", receipt.ToString());
        Assert.DoesNotContain("483920", receipt.ToString());
        if (outcome == OutboxAttemptOutcome.Accepted) Assert.Equal("explicit-number", receipt.ProviderScope);
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
        Assert.Equal(WhatsAppSendFailures.WindowClosed, expired.SafeCode); Assert.Equal(0, handler.Calls);
        var template = new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["123456"], "123456", true);
        // 200 sin el id del mensaje: Meta pudo haberlo mandado, pero no lo confirmó.
        Assert.True((await client.SendAsync(template, Ct)).IsUncertain); Assert.Equal(1, handler.Calls);
    }
    /// <summary>
    /// Lo que no llegó a Meta (DNS, conexión, TLS o el circuito abierto) vuelve a la cola; la respuesta cortada o la espera
    /// vencida (de la cola, del cliente o de la resiliencia) quedan «Sin confirmar».
    /// </summary>
    [Theory]
    [InlineData("connection", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData("dns", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData("tls", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData("circuit", OutboxAttemptOutcome.Retry, WhatsAppSendFailures.Unavailable)]
    [InlineData("cut", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData("io", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData("timeout", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData("resilience-timeout", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    [InlineData("body-cut", OutboxAttemptOutcome.Uncertain, WhatsAppCloudClient.UncertainCode)]
    public async Task Transport_failures_split_what_did_not_leave_from_what_may_have(string failure, OutboxAttemptOutcome outcome, string code)
    {
        using var handler = new Throwing(failure);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.example.test/") };
        var client = new WhatsAppCloudClient(http, Options.Create(new WhatsAppOptions { AccessToken = "fixture-token" }), TimeProvider.System);
        var receipt = await client.SendAsync(new WhatsAppOutboundMessage("number", "+5491134567890", "codigo_ingreso", "es", ["483920"], "483920", true), Ct);
        Assert.Equal((outcome, code), (receipt.Outcome, receipt.SafeCode));
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
    private sealed class DelayedHandler(TimeProvider clock, TimeSpan delay, int status, string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Reset(TimeSpan nextDelay)
        {
            delay = nextDelay;
            Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var waiting = Task.Delay(delay, clock, cancellationToken);
            Entered.TrySetResult();
            await waiting;
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(response) };
        }
    }
    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
    private sealed class Throwing(string failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => failure switch
        {
            "connection" => throw new HttpRequestException(HttpRequestError.ConnectionError),
            "dns" => throw new HttpRequestException(HttpRequestError.NameResolutionError),
            "tls" => throw new HttpRequestException(HttpRequestError.SecureConnectionError),
            "circuit" => throw new Polly.CircuitBreaker.BrokenCircuitException(),
            "cut" => throw new HttpRequestException(HttpRequestError.ResponseEnded),
            "io" => throw new IOException("reset"),
            "timeout" => throw new TaskCanceledException(),
            "resilience-timeout" => throw new Polly.Timeout.TimeoutRejectedException(),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CutStream()) }),
        };
    }

    /// <summary>Un cuerpo que se corta a mitad de la lectura.</summary>
    private sealed class CutStream : MemoryStream
    {
        public CutStream() : base("{\"messages\":[{\"id\":"u8.ToArray()) { }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position >= Length ? throw new IOException("The response ended prematurely.") : base.ReadAsync(buffer, cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) =>
            Position >= Length ? throw new IOException("The response ended prematurely.") : base.Read(buffer, offset, count);
    }

    /// <summary>El número conectado con su token cifrado; las demás lecturas no se usan acá.</summary>
    private sealed class SendingReader(WhatsAppTokenProtector protector) : ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.IWhatsAppChannelReader
    {
        private readonly Guid id = Guid.NewGuid();
        public Task<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppSendingChannelRow?> FindSendingAsync(Guid? channelId, string phoneNumberId, CancellationToken ct) =>
            Task.FromResult<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppSendingChannelRow?>(new(id, phoneNumberId, "waba", null,
                ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppNumberStatus.Connected, protector.Protect(id, "fixture-token")));
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppTemplateAccountRow>> ListTemplateAccountsAsync(bool platform, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence.WhatsAppTemplateAccountRow>> ListAllTemplateAccountsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasActiveAccountAsync(string wabaId, CancellationToken ct) => throw new NotSupportedException();
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
