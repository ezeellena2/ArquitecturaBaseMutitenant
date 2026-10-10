using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
public sealed class WhatsAppBotTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Secret = "bot-fixture-secret";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private WebApplicationFactory<Program> Host() => factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("WhatsApp:PhoneNumberId", "fixture-number"); builder.UseSetting("WhatsApp:AccessToken", "fixture-token");
        builder.UseSetting("WhatsApp:AppSecret", Secret); builder.UseSetting("WhatsApp:VerifyToken", "fixture-verify"); builder.UseSetting("WhatsApp:ProcessInboundInBackground", "false");
        builder.UseSetting("Email:AppName", "Mi plataforma");
        builder.UseSetting("WhatsApp:AllowedCountries:0", "AR");
        builder.ConfigureTestServices(services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); services.RemoveAll<IHostedService>(); });
    });
    [Fact]
    public async Task Short_and_separated_OTP_received_by_webhook_keep_no_text_and_produce_no_bot_effects()
    {
        await using var app = Host(); using var client = app.CreateClient();
        var owner = await CreatePhoneAsync(app.Services, "+5491134567899", platform: false);
        var texts = new[] { "1234", "12345", "123456", "12-34", "1 2 3 4 5", "12.34.56",
            "Mi código es 1234", "Código: 12345", "Código: 123456" };
        var prefix = Guid.NewGuid().ToString("N");
        var messages = texts.Select((text, index) => new { id = prefix + index, from = "5491134567899",
            timestamp = _clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            type = "text", text = new { body = text } }).ToArray();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { @object = "whatsapp_business_account", entry = new[] {
            new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = "fixture-number" }, messages } } } } } });
        await using var read = app.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notices = await db.OutboxMessages.CountAsync(Ct); var links = await db.Set<LoginLink>().CountAsync(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), bytes)));
        using var response = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var received = await db.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.Wamid.StartsWith(prefix)).ToArrayAsync(Ct);
        Assert.Equal(texts.Length, received.Length);
        Assert.All(received, row => { Assert.Null(row.EncryptedText); Assert.Equal(WhatsAppMessageKind.Authentication, row.Kind); Assert.Equal(owner, row.OriginalUserId); });
        foreach (var row in received)
        {
            await using var process = app.Services.CreateAsyncScope();
            var inbound = process.ServiceProvider.GetRequiredService<IWhatsAppInboundService>();
            var claimed = await inbound.ClaimAsync(Ct); Assert.NotNull(claimed.Value);
            Assert.True((await inbound.ProcessAsync(claimed.Value, Ct)).IsSuccess);
        }
        var processed = await db.Set<WhatsAppMessage>().AsNoTracking().Where(value => value.Wamid.StartsWith(prefix)).ToArrayAsync(Ct);
        Assert.All(processed, row => Assert.NotNull(row.ProcessedAtUtc));
        Assert.Equal(notices, await db.OutboxMessages.CountAsync(Ct));
        Assert.Equal(links, await db.Set<LoginLink>().CountAsync(Ct));
    }
    [Theory]
    [InlineData(BotButtons.SignIn, "/login", "5491134567892", ConsumerSignupMode.Closed)]
    [InlineData(BotButtons.Recover, "/recuperar", "5491134567892", ConsumerSignupMode.Closed)]
    [InlineData(BotButtons.SignIn, "/login", "59899123457", ConsumerSignupMode.Open)]
    [InlineData(BotButtons.Recover, "/recuperar", "59899123457", ConsumerSignupMode.Open)]
    public async Task Signed_interactive_reply_survives_queue_and_selects_only_its_web_CTA(string replyId, string path, string from, ConsumerSignupMode signup)
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        await SetSignupAsync(app.Services, signup);
        var closed = await ReceiveProcessAsync(app, client, from);
        Assert.Contains("Mi plataforma", closed.Body); Assert.Null(closed.LinkUrl);
        Assert.Equal(new[] { BotButtons.SignIn, BotButtons.Recover }, closed.ReplyButtons!.Select(value => value.Id));
        var chosen = await ReceiveProcessAsync(app, client, from, replyId);
        Assert.Equal(path, new Uri(chosen.LinkUrl!).AbsolutePath); Assert.Empty(new Uri(chosen.LinkUrl!).Query);
        Assert.Null(chosen.ReplyButtons); Assert.DoesNotContain("549", chosen.Body);
    }
    [Fact]
    public async Task Unknown_excluded_country_with_open_signup_gets_closed_choices_without_account_or_proof()
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        await SetSignupAsync(app.Services, ConsumerSignupMode.Open);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Users.CountAsync(Ct); var links = await db.Set<LoginLink>().CountAsync(Ct);
        var codes = await db.LoginCodes.CountAsync(Ct);
        var reply = await ReceiveProcessAsync(app, client, "59899123456");
        Assert.Null(reply.LinkUrl); Assert.Null(reply.LinkLabel); Assert.Contains("por ahora no se pueden crear cuentas nuevas", reply.Body);
        Assert.Equal(new[] { BotButtons.SignIn, BotButtons.Recover }, reply.ReplyButtons!.Select(value => value.Id));
        Assert.Equal(users, await db.Users.CountAsync(Ct)); Assert.Equal(links, await db.Set<LoginLink>().CountAsync(Ct));
        Assert.Equal(codes, await db.LoginCodes.CountAsync(Ct));
    }
    [Fact]
    public async Task Unknown_open_signup_is_web_only_and_creates_no_account_or_proof()
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        await SetSignupAsync(app.Services, ConsumerSignupMode.Open);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Users.CountAsync(Ct); var links = await db.Set<LoginLink>().CountAsync(Ct);
        var codes = await db.LoginCodes.CountAsync(Ct);
        var reply = await ReceiveProcessAsync(app, client, "5491134567893");
        Assert.Contains("Mi plataforma", reply.Body); Assert.Equal("/registro", new Uri(reply.LinkUrl!).AbsolutePath);
        Assert.Equal("?channel=whatsapp", new Uri(reply.LinkUrl!).Query); Assert.Empty(new Uri(reply.LinkUrl!).Fragment);
        Assert.Equal(users, await db.Users.CountAsync(Ct)); Assert.Equal(links, await db.Set<LoginLink>().CountAsync(Ct));
        Assert.Equal(codes, await db.LoginCodes.CountAsync(Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_anonymous_or_former_owner_message_never_issues_for_new_phone_owner(bool hadOwner)
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        await SetSignupAsync(app.Services, ConsumerSignupMode.Open);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var db = provider.GetRequiredService<ApplicationDbContext>();
            db.LoginMethods.RemoveRange(await db.LoginMethods.Where(value => value.Type == LoginMethodType.Phone && value.Value == "+5491134567895").ToArrayAsync(ct));
        });
        var original = hadOwner ? await CreatePhoneAsync(app.Services, "+5491134567895", false) : (Guid?)null;
        var next = Guid.Empty;
        var reply = await ReceiveProcessAsync(app, client, "5491134567895", beforeProcess: async () =>
        { next = await CreatePhoneAsync(app.Services, "+5491134567895", false); });
        await using var read = app.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.Set<LoginLink>().CountAsync(value => value.UserId == next || value.UserId == original, Ct));
        var message = await db.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == _lastWamid, Ct);
        Assert.Equal(original, message.OriginalUserId); Assert.NotEqual(next, message.OriginalUserId);
        if (hadOwner) Assert.Null(reply.LinkUrl);
        else { Assert.Equal("/registro", new Uri(reply.LinkUrl!).AbsolutePath); Assert.Empty(new Uri(reply.LinkUrl!).Fragment); }
    }
    [Theory]
    [InlineData(false, "5491134567894")]
    [InlineData(true, "5491134567894")]
    [InlineData(false, "59899123458")]
    [InlineData(true, "59899123458")]
    public async Task Verified_owner_gets_consumer_proof_while_operator_gets_only_exact_neutral_text(bool platform, string from)
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        var user = await CreatePhoneAsync(app.Services, "+" + from, platform);
        var reply = await ReceiveProcessAsync(app, client, from);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var proofs = await db.Set<LoginLink>().AsNoTracking().Where(value => value.UserId == user).ToArrayAsync(Ct);
        if (platform)
        { Assert.Equal("Para entrar, usá la web.", reply.Body); Assert.Null(reply.Footer); Assert.Null(reply.LinkLabel); Assert.Null(reply.LinkUrl); Assert.Null(reply.ReplyButtons); Assert.Empty(proofs); }
        else
        {
            Assert.Equal("/login/enlace", new Uri(reply.LinkUrl!).AbsolutePath); Assert.StartsWith("#t=", new Uri(reply.LinkUrl!).Fragment);
            Assert.Contains("Mi plataforma", reply.Body); Assert.Single(proofs);
            Assert.DoesNotContain(new Uri(reply.LinkUrl!).Fragment[3..], proofs[0].TokenHash);
            var message = await db.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.OriginalUserId == user && value.Direction == WhatsAppMessageDirection.Inbound, Ct);
            Assert.Null(message.ReplyId); Assert.DoesNotContain("#t=", scope.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(message.EncryptedText!));
        }
    }
    [Fact]
    public async Task Original_owner_suspended_before_processing_gets_no_proof_even_from_excluded_country()
    {
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        var user = await CreatePhoneAsync(app.Services, "+59899123459", false);
        var reply = await ReceiveProcessAsync(app, client, "59899123459", beforeProcess: () => TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            Assert.True((await provider.GetRequiredService<IUserRepository>().SuspendAsync(user, ct)).IsSuccess);
        }));
        Assert.Null(reply.LinkUrl); Assert.Null(reply.ReplyButtons);
        await using var read = app.Services.CreateAsyncScope();
        Assert.False(await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<LoginLink>().AnyAsync(value => value.UserId == user, Ct));
    }
    [Fact]
    public async Task Verified_owner_receives_new_proof_after_lockout_expires_on_injected_clock()
    {
        _clock.SetUtcNow(new DateTimeOffset(2100, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var app = Host(); using var client = app.CreateClient(); await app.Services.SeedDatabaseAsync(Ct);
        var user = await CreatePhoneAsync(app.Services, "+5491134567896", false);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            var account = await provider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(value => value.Id == user, ct);
            account.LockoutEnabled = true; account.LockoutEnd = _clock.GetUtcNow().AddMinutes(5);
        });
        var blocked = await ReceiveProcessAsync(app, client, "5491134567896"); Assert.Null(blocked.LinkUrl);
        _clock.Advance(TimeSpan.FromMinutes(6));
        var allowed = await ReceiveProcessAsync(app, client, "5491134567896");
        Assert.NotNull(allowed.LinkUrl); Assert.Equal("/login/enlace", new Uri(allowed.LinkUrl).AbsolutePath);
        await using var read = app.Services.CreateAsyncScope();
        Assert.Equal(1, await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<LoginLink>().CountAsync(value => value.UserId == user, Ct));
    }
    private string? _lastWamid;
    private async Task<WhatsAppOutboundMessage> ReceiveProcessAsync(WebApplicationFactory<Program> app, HttpClient client, string from, string? replyId = null, Func<Task>? beforeProcess = null)
    {
        await using var before = app.Services.CreateAsyncScope();
        var known = await before.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.Select(value => value.Id).ToArrayAsync(Ct);
        var id = Guid.NewGuid().ToString("N"); _lastWamid = id;
        object message = replyId is null
            ? new { id, from, timestamp = _clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), type = "text", text = new { body = "Hola" } }
            : new { id, from, timestamp = _clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), type = "interactive", interactive = new { type = "button_reply", button_reply = new { id = replyId, title = "Ignored label" } } };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { @object = "whatsapp_business_account", entry = new[] { new { changes = new[] { new { field = "messages", value = new { metadata = new { phone_number_id = "fixture-number" }, messages = new[] { message } } } } } } });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Hub-Signature-256", "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), bytes)));
        using var response = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.False(response.Headers.Contains("Set-Cookie"));
        await using (var acknowledged = app.Services.CreateAsyncScope())
        {
            var acknowledgedDb = acknowledged.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var durable = await acknowledgedDb.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == id, Ct);
            Assert.Null(durable.ProcessedAtUtc); Assert.Equal(replyId, durable.ReplyId);
            Assert.False(await acknowledgedDb.OutboxMessages.AnyAsync(value => !known.Contains(value.Id), Ct));
        }
        if (beforeProcess is not null) await beforeProcess();
        await using (var process = app.Services.CreateAsyncScope())
        {
            var inbound = process.ServiceProvider.GetRequiredService<IWhatsAppInboundService>(); var claimed = await inbound.ClaimAsync(Ct);
            Assert.NotNull(claimed.Value); Assert.True((await inbound.ProcessAsync(claimed.Value, Ct)).IsSuccess);
        }
        await using var read = app.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(value => !known.Contains(value.Id) && value.Channel == "whatsapp", Ct);
        Assert.Equal(OutboxStatus.Pending, row.Status);
        var inboundRow = await db.Set<WhatsAppMessage>().AsNoTracking().SingleAsync(value => value.Wamid == id, Ct);
        Assert.NotNull(inboundRow.ProcessedAtUtc); Assert.Equal(replyId, inboundRow.ReplyId);
        var payload = read.ServiceProvider.GetRequiredService<IPayloadProtector>().Unprotect(row.EncryptedPayload);
        Assert.DoesNotContain("Hola", row.EncryptedPayload); Assert.DoesNotContain(from, row.EncryptedPayload);
        return JsonSerializer.Deserialize<WhatsAppOutboundMessage>(payload)!;
    }
    private async Task<Guid> CreatePhoneAsync(IServiceProvider services, string number, bool platform)
    {
        var user = Guid.Empty;
        await TransactionAsync(services, async (provider, ct) =>
        {
            var db = provider.GetRequiredService<ApplicationDbContext>();
            db.LoginMethods.RemoveRange(await db.LoginMethods.Where(value => value.Type == LoginMethodType.Phone && value.Value == number).ToArrayAsync(ct));
        });
        await TransactionAsync(services, async (provider, ct) =>
        {
            user = (await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct)).Id;
            var method = LoginMethod.CreatePhone(user, PhoneNumber.Create(number).Value); method.Verify(_clock.GetUtcNow().UtcDateTime);
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
            if (platform) await provider.GetRequiredService<IUserRepository>().SetPlatformOperatorAsync(user, true, ct);
        }); return user;
    }
    private static Task SetSignupAsync(IServiceProvider services, ConsumerSignupMode mode) => TransactionAsync(services, async (provider, ct) =>
    {
        var repo = provider.GetRequiredService<IPlatformSettingsRepository>(); var settings = (await repo.GetAsync(ct))!;
        Assert.True(settings.Update(mode, settings.BusinessSignup, settings.MaxOwnedOrganizations, Email.Create("sales@example.test").Value, Email.Create("support@example.test").Value).IsSuccess);
        repo.MarkUpdated(settings);
        provider.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty, "Fixture bot signup", provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
    });
    private static async Task TransactionAsync(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct => { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }
}
