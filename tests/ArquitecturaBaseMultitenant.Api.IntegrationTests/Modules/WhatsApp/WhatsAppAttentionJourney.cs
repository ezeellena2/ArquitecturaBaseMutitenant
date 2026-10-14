using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>Arma lo que necesitan las pruebas de atención: el alta de Meta simulada, el módulo prendido y un webhook firmado.</summary>
internal static class WhatsAppAttentionJourney
{
    internal const string AppSecret = "attention-webhook-secret";
    internal const string Token = "graph-secret-token-0001";

    internal static WebApplicationFactory<Program> Host(ApiFactory factory, FakeGraph graph, CapturedLogs? logs = null,
        IReadOnlyDictionary<string, string>? settings = null, FakeTemplates? templates = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("WhatsApp:AppId", "app-fixture");
        builder.UseSetting("WhatsApp:EmbeddedSignupConfigId", "config-fixture");
        builder.UseSetting("WhatsApp:AppSecret", AppSecret);
        builder.UseSetting("WhatsApp:VerifyToken", "attention-verify");
        builder.UseSetting("WhatsApp:ProcessInboundInBackground", "false");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>()) builder.UseSetting(key, value);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWhatsAppGraphClient>();
            services.AddSingleton<IWhatsAppGraphClient>(graph);
            // Las plantillas nunca van a Meta en las pruebas.
            services.RemoveAll<IWhatsAppTemplateClient>();
            services.AddSingleton<IWhatsAppTemplateClient>(templates ?? new FakeTemplates());
            if (logs is not null) services.AddSingleton<ILoggerProvider>(logs);
        });
    });

    internal static HttpClient Client(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Una organización con su Dueño (que recibe los permisos de WhatsApp) y el módulo prendido.</summary>
    internal static async Task<(AccountJourney Owner, Guid TenantId)> OrganizationAsync(ApiFactory factory, WebApplicationFactory<Program> host,
        HttpClient client, CancellationToken ct, bool enable = true)
    {
        var owner = await AccountJourney.RegisterAsync(host, client, ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "WhatsApp " + Guid.NewGuid().ToString("N")[..8], ct);
        if (enable) await EnableAsync(factory, tenant, ct);
        await AccountJourney.AuthorizeAsync(client, "business", ct, tenant);
        return (owner, tenant);
    }

    internal static async Task EnableAsync(ApiFactory factory, Guid tenantId, CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, WhatsAppFeature.Key, true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty,
                tenantId, "WhatsApp attention fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(tenantId, ct);
    }

    internal static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, CancellationToken ct, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request, ct);
    }

    internal static Task<HttpResponseMessage> ConnectAsync(HttpClient client, string phoneNumberId, CancellationToken ct, string? key = null,
        Guid? companyId = null) =>
        PostAsync(client, "/api/organization/whatsapp/numbers",
            new { code = "signup-code-" + phoneNumberId, phoneNumberId, wabaId = "waba-" + phoneNumberId, companyId, coexistence = false }, ct, key);

    internal static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.Clone();
    }

    internal static async Task<HttpResponseMessage> WebhookAsync(HttpClient client, string body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret),
            Encoding.UTF8.GetBytes(body))).ToLowerInvariant());
        return await client.SendAsync(request, ct);
    }

    internal static string Statuses(string phoneNumberId, params object[] statuses) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
        {
            messaging_product = "whatsapp",
            metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
            statuses,
        } } } } },
    });

    internal static object Status(string wamid, string status, string? category = null, string? type = null, int? error = null) => new
    {
        id = wamid, status, recipient_id = "5491155550000",
        timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        pricing = category is null ? null : new { billable = type == "regular", pricing_model = "PMP", type, category },
        errors = error is null ? null : new[] { new { code = error } },
    };

    /// <summary>Un cliente le escribe un texto al número (con la hora de Meta que se pida) y el procesador lo atiende.</summary>
    internal static async Task ReceiveAsync(WebApplicationFactory<Program> host, HttpClient client, string phoneNumberId, string from, string text,
        CancellationToken ct, DateTimeOffset? at = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[] { new { id = "waba-" + phoneNumberId, changes = new[] { new { field = "messages", value = new
            {
                messaging_product = "whatsapp",
                metadata = new { display_phone_number = "5491100000000", phone_number_id = phoneNumberId },
                messages = new[] { new
                {
                    from = from.TrimStart('+'), id = "wamid.journey." + Guid.NewGuid().ToString("N"), type = "text", text = new { body = text },
                    timestamp = (at ?? TimeProvider.System.GetUtcNow()).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                } },
            } } } } },
        });
        using var received = await WebhookAsync(client, body, ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, received.StatusCode);
        await host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor>().Single().ProcessPendingAsync(ct);
    }

    /// <summary>Un móvil de AMBA que el webhook acepta: un abonado que empieza con 0 o con 1 no es válido y se descartaría.</summary>
    internal static string NewPhone() => "+54911" + RandomNumberGenerator.GetInt32(30_000_000, 59_999_999)
        .ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string NewNumberId() => "pn" + RandomNumberGenerator.GetInt32(100_000_000, int.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>El alta de Meta simulada: cuenta las llamadas y responde con un token que nunca debe aparecer en una respuesta ni en un log.</summary>
internal sealed class FakeGraph : IWhatsAppGraphClient
{
    public int Signups;
    public bool HasPayment { get; set; } = true;
    public Error? Failure { get; set; }

    public Task<Result<WhatsAppSignupResult>> CompleteSignupAsync(WhatsAppSignupInput input, CancellationToken ct)
    {
        Interlocked.Increment(ref Signups);
        if (Failure is { } failure) return Task.FromResult(Result.Failure<WhatsAppSignupResult>(failure));
        var phoneNumberId = input.PhoneNumberId ?? "pn" + string.Concat(input.WabaId.Where(char.IsAsciiDigit));
        var digits = string.Concat(phoneNumberId.Where(char.IsAsciiDigit));
        var display = PhoneNumber.Create("+54911" + digits[^8..]).Value;
        return Task.FromResult(Result.Success(new WhatsAppSignupResult(phoneNumberId, WhatsAppAttentionJourney.Token, input.Coexistence ? null : "123456",
            display, "Negocio verificado", HasPayment)));
    }

    public Task<Result<bool>> HasPaymentAsync(string wabaId, string token, CancellationToken ct)
    {
        Assert.Equal(WhatsAppAttentionJourney.Token, token);
        return Task.FromResult(Result.Success(HasPayment));
    }
}

/// <summary>Las plantillas de Meta simuladas: guarda lo que se mandó y se borró, y lista lo que cada cuenta tiene.</summary>
internal sealed class FakeTemplates : IWhatsAppTemplateClient
{
    private long next = 7_000_000;
    public ConcurrentQueue<(string WabaId, WhatsAppTemplateSubmission Submission)> Created { get; } = new();
    public ConcurrentQueue<(string WabaId, string MetaName)> Deleted { get; } = new();
    public ConcurrentDictionary<string, IReadOnlyList<WhatsAppMetaTemplate>> Accounts { get; } = new(StringComparer.Ordinal);
    public Error? Failure { get; set; }

    public Task<Result<WhatsAppMetaTemplate>> CreateAsync(string wabaId, string token, WhatsAppTemplateSubmission submission, CancellationToken ct)
    {
        Assert.False(string.IsNullOrEmpty(token));
        if (Failure is { } failure) return Task.FromResult(Result.Failure<WhatsAppMetaTemplate>(failure));
        Created.Enqueue((wabaId, submission));
        return Task.FromResult(Result.Success(new WhatsAppMetaTemplate(Interlocked.Increment(ref next).ToString(System.Globalization.CultureInfo.InvariantCulture),
            submission.MetaName, submission.MetaLanguage, WhatsAppTemplateStatus.Pending, submission.Category, null)));
    }

    public Task<Result> DeleteAsync(string wabaId, string token, string metaName, CancellationToken ct)
    {
        Deleted.Enqueue((wabaId, metaName));
        return Task.FromResult(Result.Success());
    }

    public Task<Result<IReadOnlyList<WhatsAppMetaTemplate>>> ListAsync(string wabaId, string token, CancellationToken ct) =>
        Task.FromResult(Result.Success(Accounts.TryGetValue(wabaId, out var templates) ? templates : (IReadOnlyList<WhatsAppMetaTemplate>)[]));
}

internal sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Logger(this);
    public void Dispose() { }
    private sealed class Logger(CapturedLogs owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (state is IEnumerable<KeyValuePair<string, object?>> fields) text += string.Join(";", fields.Select(value => value.Value?.ToString()));
            owner.Entries.Enqueue(text + exception);
        }
    }
}
