using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>Comandos privados: API, ownership, outbox cifrado y xmin reales; transportes prohibidos.</summary>
public sealed class WhatsAppBrowserPrivateCommandsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("1440", "+5491134567860")]
    [InlineData("390", "+5491134567861")]
    public async Task Pending_backup_email_code_from_real_http_verifies_without_changing_primary_phone(string width, string phone)
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using var app = Host(clock);
        using var client = Client(app);
        var control = new WhatsAppBrowserControl(app.Services);
        var userId = await SignupPhoneAsync(app, client, control, phone);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        var request = new WhatsAppBrowserRequest("begin", "backup-email-" + width, phone);
        await control.ExecuteAsync(request, client, Ct);
        var email = "e8-backup-" + width + "@example.test";
        using var added = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
        var methodId = (await JsonAsync(added)).GetProperty("methodId").GetGuid();
        var read = request with { Kind = "methodEmailCode", Email = email, MethodId = methodId };
        var proof = await control.ExecuteAsync(read, client, Ct);
        Assert.DoesNotContain(email, proof.ToString()!, StringComparison.Ordinal);
        var reply = JsonSerializer.SerializeToElement(proof);
        Assert.Single(reply.EnumerateObject());
        var ownCode = reply.GetProperty("proof").GetString()!;
        var foreignCode = (ownCode[0] == '9' ? '0' : (char)(ownCode[0] + 1)) + ownCode[1..];
        clock.Advance(TimeSpan.FromSeconds(1));
        await using (var foreignScope = app.Services.CreateAsyncScope())
        {
            var provider = foreignScope.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var foreignUser = await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct);
                provider.GetRequiredService<IOutbox>().Enqueue("email", JsonSerializer.Serialize(new { To = email, TextBody = foreignCode + "\n" }), foreignUser.Id);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        var selected = JsonSerializer.SerializeToElement(await control.ExecuteAsync(read, client, Ct));
        Assert.True(selected.GetProperty("proof").GetString() == ownCode, "Only the current owner's OTP may be selected.");
        using var verified = await AccountJourney.PostAsync(client, $"/api/me/login-methods/{methodId:D}/verify",
            new { code = selected.GetProperty("proof").GetString() }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(read, client, Ct));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.LoginMethods.AnyAsync(method => method.Id == methodId && method.VerifiedAtUtc != null && !method.IsPrimary, Ct));
        Assert.True(await db.LoginMethods.AnyAsync(method => method.UserId == userId && method.Type == LoginMethodType.Phone
            && method.Value == phone && method.IsPrimary && method.VerifiedAtUtc != null, Ct));
        Assert.True(await db.OutboxMessages.AnyAsync(row => row.UserId == userId && row.Channel == "email" && row.EncryptedPayload != "", Ct));
    }

    [Fact]
    public async Task Email_proof_rejects_history_foreign_owner_and_changed_primary_without_exposing_or_dispatching()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await using var app = Host(clock, isolated);
        using var client = Client(app);
        var control = new WhatsAppBrowserControl(app.Services);
        const string phone = "+5491134567862";
        var userId = await SignupPhoneAsync(app, client, control, phone);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        const string email = "e8-backup-1440@example.test";
        using var added = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
        var methodId = (await JsonAsync(added)).GetProperty("methodId").GetGuid();
        clock.Advance(TimeSpan.FromSeconds(1));
        var begin = new WhatsAppBrowserRequest("begin", "backup-email-1440", phone);
        await control.ExecuteAsync(begin, client, Ct);
        var read = begin with { Kind = "methodEmailCode", Email = email, MethodId = methodId };
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(read, client, Ct));
        using var removed = await client.DeleteAsync($"/api/me/login-methods/{methodId:D}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var ownAdded = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, ownAdded.StatusCode);
        var ownId = (await JsonAsync(ownAdded)).GetProperty("methodId").GetGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(read with { MethodId = Guid.NewGuid() }, client, Ct));
        // Un método pendiente ajeno con el destino sintético correcto tampoco se puede leer.
        using var otherClient = Client(app);
        var otherId = await SignupPhoneAsync(app, otherClient, control, "+5491134567863");
        await MutateAsync(app.Services, async (db, ct) =>
        {
            db.LoginMethods.Remove(await db.LoginMethods.SingleAsync(method => method.Id == ownId, ct));
        });
        await MutateAsync(app.Services, (db, _) =>
        {
            db.LoginMethods.Add(LoginMethod.CreateEmail(otherId, Email.Create(email).Value));
            return Task.CompletedTask;
        });
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var foreignId = await db.LoginMethods.Where(method => method.Value == email).Select(method => method.Id).SingleAsync(Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(read with { MethodId = foreignId }, client, Ct));
        var before = await db.OutboxMessages.CountAsync(Ct);
        await MutateAsync(app.Services, async (context, ct) =>
        {
            var method = await context.LoginMethods.SingleAsync(method => method.UserId == userId && method.Value == phone, ct);
            method.ClearPrimary();
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(read with { MethodId = foreignId }, client, Ct));
        Assert.Equal(before, await db.OutboxMessages.CountAsync(Ct));
        Assert.All(await db.OutboxMessages.AsNoTracking().ToArrayAsync(Ct), row => Assert.Equal(0, row.Attempts));
    }

    [Fact]
    public async Task Real_notice_preserves_ui_version_repeats_without_outbox_and_allows_http_approval()
    {
        await using var app = Host();
        using var owner = Client(app);
        using var anonymous = Client(app);
        var control = new WhatsAppBrowserControl(app.Services);
        var userId = await SignupPhoneAsync(app, owner, control, "+5491134567864");
        using var platform = await OperatorAsync(app, control);
        var begin = new WhatsAppBrowserRequest("begin", "recovered-1440", "+5491134567865");
        await control.ExecuteAsync(begin, anonymous, Ct);
        await SubmitRecoveryAsync(anonymous, control, begin, "+5491134567864");
        using var listed = await platform.GetAsync("/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(begin.Phone), Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var row = Assert.Single((await JsonAsync(listed)).GetProperty("items").EnumerateArray());
        var id = row.GetProperty("id").GetGuid();
        var version = row.GetProperty("version").GetUInt32();
        var notice = begin with { Kind = "recoveryNotice", RecoveryId = id };
        var reply = JsonSerializer.SerializeToElement(await control.ExecuteAsync(notice, anonymous, Ct));
        Assert.Equal(new[] { "afterVersion", "beforeVersion", "marked", "queuedNotices" }, reply.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.True(reply.GetProperty("marked").GetBoolean());
        Assert.True(reply.GetProperty("queuedNotices").GetInt32() > 0);
        Assert.Equal(version, reply.GetProperty("beforeVersion").GetUInt32());
        Assert.Equal(version, reply.GetProperty("afterVersion").GetUInt32());
        var repeated = JsonSerializer.SerializeToElement(await control.ExecuteAsync(notice, anonymous, Ct));
        Assert.True(repeated.GetProperty("marked").GetBoolean());
        Assert.Equal(0, repeated.GetProperty("queuedNotices").GetInt32());
        Assert.Equal(version, repeated.GetProperty("afterVersion").GetUInt32());
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.AccountRecoveryNotices.AnyAsync(value => value.Id == id, Ct));
        Assert.True(await db.OutboxMessages.AnyAsync(value => value.UserId == userId && value.EncryptedPayload != "" && value.Attempts == 0, Ct));
        Assert.Equal(AccountRecoveryStatus.Pending, (await db.AccountRecoveryRequests.AsNoTracking().SingleAsync(value => value.Id == id, Ct)).Status);
        using var approved = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
            new { version, reason = "Prueba privada del aviso real" }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(AccountRecoveryStatus.Approved, (await db.AccountRecoveryRequests.AsNoTracking().SingleAsync(value => value.Id == id, Ct)).Status);
    }

    [Fact]
    public async Task Recovery_notice_rejects_foreign_new_phone_and_history_before_calling_the_service()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using var app = Host(clock);
        using var owner = Client(app);
        using var anonymous = Client(app);
        var control = new WhatsAppBrowserControl(app.Services);
        await SignupPhoneAsync(app, owner, control, "+5491134567866");
        var history = new WhatsAppBrowserRequest("begin", "old-recovery", "+5491134567867");
        await control.ExecuteAsync(history, anonymous, Ct);
        await SubmitRecoveryAsync(anonymous, control, history, "+5491134567866");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await db.AccountRecoveryRequests.Where(row => row.NewMethodValue == history.Phone).Select(row => row.Id).SingleAsync(Ct);
        clock.Advance(TimeSpan.FromSeconds(1));
        var late = history with { Scenario = "rejected-1440" };
        await control.ExecuteAsync(late, anonymous, Ct);
        var foreign = history with { Scenario = "foreign-recovery", Phone = "+5491134567868" };
        await control.ExecuteAsync(foreign, anonymous, Ct);
        var before = await db.OutboxMessages.CountAsync(Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(late with { Kind = "recoveryNotice", RecoveryId = id }, anonymous, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(foreign with { Kind = "recoveryNotice", RecoveryId = id }, anonymous, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => control.ExecuteAsync(history with { Kind = "recoveryNotice", RecoveryId = Guid.NewGuid() }, anonymous, Ct));
        Assert.False(await db.AccountRecoveryNotices.AnyAsync(value => value.Id == id, Ct));
        Assert.Equal(before, await db.OutboxMessages.CountAsync(Ct));
    }

    private WebApplicationFactory<Program> Host(TimeProvider? clock = null, ApiFactory? isolated = null) => (isolated ?? factory).WithWebHostBuilder(builder => builder
        .UseSetting("WhatsApp:PhoneNumberId", WhatsAppBrowserControl.NumberId).UseSetting("WhatsApp:AccessToken", "fixture-token")
        .UseSetting("WhatsApp:AppSecret", WhatsAppBrowserControl.AppSecret).UseSetting("WhatsApp:VerifyToken", "fixture-verify")
        .UseSetting("WhatsApp:ProcessInboundInBackground", "false")
        .UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0").UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "100")
        .UseSetting("Seed:PlatformOwner:Email", WhatsAppBrowserControl.OperatorEmail)
        .UseSetting("Seed:PlatformOwner:Phone", WhatsAppBrowserControl.OperatorPhone)
        .ConfigureLogging(logging => logging.ClearProviders()).ConfigureTestServices(services =>
        {
            services.RemoveAll<IChannelSender>();
            services.AddSingleton<IChannelSender>(new ForbiddenSender("email"));
            services.AddSingleton<IChannelSender>(new ForbiddenSender("whatsapp"));
            if (clock is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(clock); }
        }));

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static object Input(string phone) => new { country = "AR", number = phone };
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return document!.RootElement.Clone();
    }

    private static async Task<Guid> SignupPhoneAsync(WebApplicationFactory<Program> app, HttpClient client, WhatsAppBrowserControl control, string phone)
    {
        await app.Services.SeedDatabaseAsync(Ct);
        var begin = new WhatsAppBrowserRequest("begin", "signup-" + phone[^2..], phone);
        await control.ExecuteAsync(begin, client, Ct);
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/signup", new { phone = Input(phone), channel = "whatsapp", acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = JsonSerializer.SerializeToElement(await control.ExecuteAsync(begin with { Kind = "code" }, client, Ct)).GetProperty("proof").GetString();
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/signup/verify", new { phone = Input(phone), channel = "whatsapp", acceptedTerms = true, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.Where(method => method.Value == phone).Select(method => method.UserId).SingleAsync(Ct);
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> app, WhatsAppBrowserControl control)
    {
        var client = Client(app);
        var begin = new WhatsAppBrowserRequest("begin", "operator-notice", WhatsAppBrowserControl.OperatorPhone);
        await control.ExecuteAsync(begin, client, Ct);
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = WhatsAppBrowserControl.OperatorEmail }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = JsonSerializer.SerializeToElement(await control.ExecuteAsync(begin with { Kind = "operatorCode" }, client, Ct)).GetProperty("proof").GetString();
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email = WhatsAppBrowserControl.OperatorEmail, code, returnUrl = "/connect/authorize?access=platform" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var me = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var documents = (await JsonAsync(me)).GetProperty("pendingLegalDocuments").EnumerateArray()
            .Select(document => new { id = document.GetProperty("id").GetGuid(), version = document.GetProperty("version").GetInt32() }).ToArray();
        using var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        return client;
    }

    private static async Task SubmitRecoveryAsync(HttpClient client, WhatsAppBrowserControl control, WhatsAppBrowserRequest begin, string previousPhone)
    {
        var nonce = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var requested = await AccountJourney.PostAsync(client, "/api/account-recovery/code", new { phone = Input(begin.Phone), channel = "whatsapp", nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = JsonSerializer.SerializeToElement(await control.ExecuteAsync(begin with { Kind = "code" }, client, Ct)).GetProperty("proof").GetString();
        using var verified = await AccountJourney.PostAsync(client, "/api/account-recovery/verify", new { phone = Input(begin.Phone), channel = "whatsapp", nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(client, "/api/account-recovery", new { phone = Input(begin.Phone), channel = "whatsapp", nonce, previousPhone = Input(previousPhone) }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
    }

    private static async Task MutateAsync(IServiceProvider services, Func<ApplicationDbContext, CancellationToken, Task> work)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            await work(provider.GetRequiredService<ApplicationDbContext>(), ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private sealed class ForbiddenSender(string key) : IChannelSender
    {
        public string Key => key;
        public Task SendAsync(string payload, CancellationToken cancellationToken) => throw new InvalidOperationException("External transport is forbidden in this fixture.");
    }
}
