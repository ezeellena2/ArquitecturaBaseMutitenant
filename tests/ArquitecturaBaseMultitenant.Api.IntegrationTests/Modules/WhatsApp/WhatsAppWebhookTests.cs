using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppWebhookTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Secret = "webhook-fixture-secret";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private WebApplicationFactory<Program> Host(CommitProbe? probe = null) => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("WhatsApp:PhoneNumberId", "fixture-number");
        builder.UseSetting("WhatsApp:AccessToken", "fixture-access");
        builder.UseSetting("WhatsApp:AppSecret", Secret);
        builder.UseSetting("WhatsApp:VerifyToken", "fixture-verify");
        builder.UseSetting("WhatsApp:ProcessInboundInBackground", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWhatsAppInboundConsumer>();
            if (probe is not null)
            {
                services.RemoveAll<IUnitOfWork>();
                services.AddScoped<IUnitOfWork>(provider => new ObservedUnitOfWork(
                    new UnitOfWork(provider.GetRequiredService<ApplicationDbContext>(), provider.GetRequiredService<ITenantContext>(), provider.GetRequiredService<ILogger<UnitOfWork>>()),
                    provider.GetRequiredService<ApplicationDbContext>(), probe));
            }
        });
    });

    [Fact]
    public async Task Exact_bytes_require_their_own_signature_and_invalid_challenges_are_rejected()
    {
        await using var app = Host(); using var client = app.CreateClient();
        const string original = "{\"object\":\"whatsapp_business_account\",\"entry\":[],\"ignored\":\"\\u00e1\"}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new StringContent(original + " ", Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(original))));
        using var altered = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, altered.StatusCode);
        using var exact = await SendAsync(client, original, true); Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        foreach (var query in new[] { "hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=ok", "hub.mode=other&hub.verify_token=fixture-verify&hub.challenge=ok", "hub.mode=subscribe&hub.verify_token=fixture-verify" })
        { using var response = await client.GetAsync("/webhooks/whatsapp?" + query, Ct); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    }

    [Fact]
    public async Task Ack_waits_for_commit_and_real_unique_rollback_replays_other_new_items_in_fresh_scope()
    {
        var probe = new CommitProbe();
        await using var app = Host(probe); using var client = app.CreateClient();
        var existingId = Guid.NewGuid().ToString("N");
        using var existing = await SendAsync(client, Envelope([Message(existingId, "Primero")]), true);
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        var newId = Guid.NewGuid().ToString("N");
        probe.DuplicateWamid = existingId; probe.Enabled = true;
        var responseTask = SendAsync(client, Envelope([Message(existingId, "Repetido"), Message(newId, "Nuevo")]), true);
        await probe.BeforeCommit.Task.WaitAsync(Ct);
        Assert.False(responseTask.IsCompleted);
        await using (var read = app.Services.CreateAsyncScope())
            Assert.False(await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AnyAsync(value => value.Wamid == newId, Ct));
        probe.Release.SetResult();
        using var response = await responseTask; Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, probe.ContextIds.Distinct().Count());
        await using var final = app.Services.CreateAsyncScope();
        Assert.Equal(2, await final.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().CountAsync(value => value.Wamid == existingId || value.Wamid == newId, Ct));
    }

    [Fact]
    public async Task Webhook_rate_limit_uses_module_code_localized_detail_and_retry_metadata()
    {
        await using var app = Host(); using var client = app.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        for (var index = 0; index < 120; index++)
        { using var response = await SendAsync(client, Envelope([]), true); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using var rejected = await SendAsync(client, Envelope([]), true);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("WhatsApp.Webhook.TooManyRequests", problem?["code"]?.GetValue<string>());
        Assert.NotNull(problem?["traceId"]); Assert.True(problem?["retryAfter"]?.GetValue<int>() > 0);
        Assert.NotNull(rejected.Headers.RetryAfter); Assert.Contains("requests", problem?["detail"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signature_is_checked_before_json_binding_and_challenge_opens_no_session()
    {
        await using var application = Host();
        using var client = application.CreateClient(new() { HandleCookies = false });
        using var invalid = await SendAsync(client, "{broken", signed: false);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal("WhatsApp.Webhook.InvalidSignature", (await invalid.Content.ReadFromJsonAsync<JsonObject>(Ct))?["code"]?.GetValue<string>());
        using var malformed = await SendAsync(client, "{broken", signed: true);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("WhatsApp.Webhook.InvalidPayload", (await malformed.Content.ReadFromJsonAsync<JsonObject>(Ct))?["code"]?.GetValue<string>());
        using var valid = await SendAsync(client, " { \"object\" : \"whatsapp_business_account\", \"entry\":[] } ", signed: true);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.False(valid.Headers.Contains("Set-Cookie"));
        using var challenge = await client.GetAsync("/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=fixture-verify&hub.challenge=12345", Ct);
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        Assert.Equal("12345", await challenge.Content.ReadAsStringAsync(Ct));
        Assert.False(challenge.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1L)]
    [InlineData(6L * 1024 * 1024)]
    public async Task Real_byte_limit_precedes_json_even_when_length_is_missing_or_false(long? reportedLength)
    {
        await using var application = Host();
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new LyingContent(new byte[5 * 1024 * 1024 + 1], reportedLength) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("WhatsApp.Webhook.PayloadTooLarge", problem?["code"]?.GetValue<string>());
        Assert.NotNull(problem?["traceId"]);
    }

    [Fact]
    public async Task Batch_is_durable_deduplicated_and_uses_only_the_configured_destination()
    {
        await using var application = Host(); using var client = application.CreateClient();
        var prefix = Guid.NewGuid().ToString("N");
        var body = Envelope([Message(prefix + "-safe", "Hola, qué tal"), Message(prefix + "-otp", "123 456"),
            Message(prefix + "-url", "https://example.test/auth?token=private"), Message(prefix + "-token", new string('a', 43)),
            new { unexpected = true }, Message(prefix + "-safe", "duplicate")]);
        using var first = await SendAsync(client, body, true);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var replays = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(client, body, true)));
        foreach (var replay in replays) { using (replay) Assert.Equal(HttpStatusCode.OK, replay.StatusCode); }
        using var foreign = await SendAsync(client, Envelope([Message(prefix + "-foreign", "Hola")], number: "foreign-number"), true);
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await context.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.Wamid.StartsWith(prefix)).ToArrayAsync(Ct);
        Assert.Equal(4, rows.Length);
        var safe = Assert.Single(rows, value => value.Wamid == prefix + "-safe");
        Assert.Equal("Hola, qué tal", scope.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(safe.EncryptedText!));
        Assert.All(rows.Where(value => value != safe), row => { Assert.Null(row.EncryptedText); Assert.Equal(WhatsAppMessageKind.Authentication, row.Kind); });
        Assert.All(rows, row => { Assert.Null(row.OriginalUserId); Assert.Null(row.OriginalMethodId); Assert.Equal(0, row.Attempts); Assert.Null(row.ProcessedAtUtc); });
        Assert.Null((await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().ClaimAsync(Ct)).Value);
    }

    [Fact]
    public async Task Received_owner_is_captured_from_verified_phone_and_replay_cannot_reassign_it()
    {
        await using var app = Host(); using var client = app.CreateClient();
        Guid owner = Guid.Empty; Guid methodId = Guid.Empty;
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                owner = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                var method = LoginMethod.CreatePhone(owner, PhoneNumber.Create("+5491134567891").Value);
                method.Verify(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime); methodId = method.Id;
                scope.ServiceProvider.GetRequiredService<ILoginMethodRepository>().Add(method); return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        var id = Guid.NewGuid().ToString("N");
        var body = Envelope([new { id, from = "5491134567891", timestamp = "1791400000", type = "text", text = new { body = "Hola" } }]);
        using var received = await SendAsync(client, body, true); Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.LoginMethods.Remove(await context.LoginMethods.SingleAsync(value => value.Id == methodId, ct));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        using var replay = await SendAsync(client, body, true); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await using var read = app.Services.CreateAsyncScope();
        var row = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == id, Ct);
        Assert.Equal(owner, row.OriginalUserId); Assert.Equal(methodId, row.OriginalMethodId);
    }

    [Fact]
    public async Task Early_callbacks_are_retained_and_reconciled_without_contact_or_owner_transfer()
    {
        await using var application = Host(); using var client = application.CreateClient();
        var id = Guid.NewGuid().ToString("N"); var owner = Guid.NewGuid();
        using var callbacks = await SendAsync(client, Envelope([], [Status(id, "read", "1791400003"), Status(id, "delivered", "1791400002")]), true);
        Assert.Equal(HttpStatusCode.OK, callbacks.StatusCode);
        await using (var scope = application.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var outbox = OutboxMessage.Enqueue("observer-fixture", "ciphertext", new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), owner);
                context.OutboxMessages.Add(outbox);
                // Guardar primero como lo hace Complete en un caso posterior.
                return Task.FromResult(Result.Success(outbox.Id));
            }, CommitPolicy.OnSuccess, Ct);
        }
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.UserId == owner, Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await scope.ServiceProvider.GetServices<IOutboxDeliveryObserver>().Single(value => value.Key == "whatsapp").RecordAcceptedAsync(outbox.Id,
                    OutboxDeliveryReceipt.Accepted(id, "fixture-number", "Authentication"), new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var late = await SendAsync(client, Envelope([], [Status(id, "sent", "1791400010")]), true);
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        await using var read = application.Services.CreateAsyncScope();
        var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking().Where(value => value.Wamid == id).ToArrayAsync(Ct);
        Assert.Equal(4, rows.Length);
        var accepted = Assert.Single(rows, value => value.EventType == "accepted");
        Assert.Equal(WhatsAppMessageStatus.Read, accepted.Status);
        Assert.Equal(owner, accepted.OriginalUserId);
        Assert.All(rows.Where(value => value.EventType is "read" or "delivered"), value => Assert.Null(value.OriginalUserId));
        Assert.All(rows, value => { Assert.Null(value.ContactId); Assert.Null(value.EncryptedText); Assert.NotNull(value.ProcessedAtUtc); });
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12-34-56")]
    [InlineData("1 2 3 4 5 6")]
    [InlineData("Bearer abc")]
    public async Task Common_credentials_are_metadata_only(string text)
    {
        await using var application = Host(); using var client = application.CreateClient();
        var id = Guid.NewGuid().ToString("N");
        using var response = await SendAsync(client, Envelope([Message(id, text)]), true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var message = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == id, Ct);
        Assert.Null(message.EncryptedText); Assert.Equal(WhatsAppMessageKind.Authentication, message.Kind);
    }

    private static object Message(string id, string text) => new { id, from = "5491134567890", timestamp = "1791400000", type = "text", text = new { body = text } };
    private static object Status(string id, string state, string time) => new { id, status = state, timestamp = time };
    private static string Envelope(object[] messages, object[]? statuses = null, string number = "fixture-number") => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = number }, messages, statuses = statuses ?? [] } } } } },
    });

    private sealed class LyingContent(byte[] bytes, long? length) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long value) { value = length ?? 0; return length.HasValue; }
    }

    private sealed class CommitProbe
    {
        internal bool Enabled { get; set; }
        internal string? DuplicateWamid { get; set; }
        internal TaskCompletionSource BeforeCommit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal List<Guid> ContextIds { get; } = [];
    }
    private sealed class ObservedUnitOfWork(IUnitOfWork inner, ApplicationDbContext context, CommitProbe probe) : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CommitPolicy policy, CancellationToken ct) where T : Result =>
            inner.ExecuteInTransactionAsync(async cancellationToken =>
            {
                var result = await work(cancellationToken);
                if (probe.Enabled && context.ChangeTracker.Entries<WhatsAppMessage>().Any(value => value.State == EntityState.Added))
                {
                    probe.ContextIds.Add(context.ContextId.InstanceId);
                    if (probe.DuplicateWamid is { } duplicate)
                    {
                        probe.DuplicateWamid = null;
                        probe.BeforeCommit.SetResult(); await probe.Release.Task.WaitAsync(cancellationToken);
                        // Una violación real de PostgreSQL revierte también el otro elemento nuevo del lote.
                        context.Set<WhatsAppMessage>().Add(WhatsAppMessage.Create("fixture-number", duplicate, "inbound", WhatsAppMessageDirection.Inbound,
                            WhatsAppMessageKind.Text, WhatsAppMessageStatus.Pending, new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)));
                    }
                }
                return result;
            }, policy, ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string body, bool signed)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (signed) request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), bytes)));
        return await client.SendAsync(request, Ct);
    }
}
