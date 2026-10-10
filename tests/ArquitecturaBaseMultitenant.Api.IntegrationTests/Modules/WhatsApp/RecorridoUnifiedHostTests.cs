using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Recorrido;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>El host privado comparte PG externo real, correo pickup, sesión antigua y servicios WhatsApp.</summary>
public sealed class RecorridoUnifiedHostTests(ApiFactory postgres) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Redirect = new("https://localtest.me:5174/auth/callback");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task External_host_preserves_business_email_cooldown_after_invalid_attempt_without_replacing_original_code()
    {
        var directory = Workspace();
        try
        {
            await using var external = ApiFactory.ForIsolatedDatabase(Connections());
            await external.InitializeDatabaseAsync(Ct);
            await using var app = RecorridoApiHost.Configure(external, Root(), directory, RecorridoWhatsAppModule.Configure);
            using var client = Client(app);
            var email = Email.Create($"walkthrough-cooldown-{Guid.NewGuid():N}@example.test").Value;
            // Cuenta verificada preexistente, como E7Preparation: no emite un OTP previo ni altera su reloj.
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var users = services.GetRequiredService<IUserRepository>();
                await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
                {
                    var user = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
                    var method = LoginMethod.CreateEmail(user.Id, email);
                    method.Verify(nowUtc);
                    Assert.True(method.MakePrimary().IsSuccess);
                    services.GetRequiredService<ILoginMethodRepository>().Add(method);
                    await users.SetPrimaryEmailAsync(user.Id, email, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, Ct);
                Assert.False(await services.GetRequiredService<ApplicationDbContext>().LoginCodes
                    .AnyAsync(row => row.Destination == email.Value, Ct));
            }

            using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct);
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var requestedBody = await JsonAsync(requested);
            var originalCode = await PickupAsync(app.Services, directory, email.Value);
            var wrongCode = originalCode == "000000" ? "111111" : "000000";
            using var invalid = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
                new { email = email.Value, code = wrongCode, returnUrl = "/connect/authorize?client_id=web&access=business" }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var invalidBody = await JsonAsync(invalid);
            Assert.Equal("Auth.LoginCode.Invalid", invalidBody.GetProperty("code").GetString());
            Assert.Equal(4, invalidBody.GetProperty("attemptsLeft").GetInt32());
            Assert.False(invalid.Headers.Contains("Set-Cookie"));

            await using var readScope = app.Services.CreateAsyncScope();
            var db = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original = Assert.Single(await db.LoginCodes.AsNoTracking().Where(row => row.Destination == email.Value).ToArrayAsync(Ct));
            Assert.Equal(1, original.FailedAttempts);
            Assert.Null(original.InvalidatedAtUtc);
            Assert.Null(original.ConsumedAtUtc);
            var emailOutbox = await db.OutboxMessages.AsNoTracking().Where(row => row.Channel == "email")
                .Select(row => row.Id).OrderBy(id => id).ToArrayAsync(Ct);

            // PostAsync genera otra clave de idempotencia; se evalúa el cooldown real, no un replay del 202.
            using var resend = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, resend.StatusCode);
            var resendBody = await JsonAsync(resend);
            Assert.Equal("Auth.LoginCode.ResendTooSoon", resendBody.GetProperty("code").GetString());
            var retryAfter = resendBody.GetProperty("retryAfter").GetInt32();
            Assert.InRange(retryAfter, 1, 60);
            Assert.Equal(TimeSpan.FromSeconds(retryAfter), resend.Headers.RetryAfter!.Delta);
            Assert.False(resend.Headers.Contains("Set-Cookie"));
            Assert.Equal(60, requestedBody.GetProperty("resendAfterSeconds").GetInt32());
            Assert.Equal(60, app.Services.GetRequiredService<IOptions<LoginCodeOptions>>().Value.ResendCooldownSeconds);
            Assert.Equal(postgres.RuntimeConnectionString, app.Services.GetRequiredService<IConfiguration>().GetConnectionString("appdb"));
            Assert.Equal("https://localtest.me:5174/", app.Services.GetRequiredService<IConfiguration>()["Authentication:Issuer"]);

            var preserved = Assert.Single(await db.LoginCodes.AsNoTracking().Where(row => row.Destination == email.Value).ToArrayAsync(Ct));
            Assert.Equal(original.Id, preserved.Id);
            Assert.Equal(original.CodeHash, preserved.CodeHash);
            Assert.Equal(original.CreatedAtUtc, preserved.CreatedAtUtc);
            Assert.Equal(original.ExpiresAtUtc, preserved.ExpiresAtUtc);
            Assert.Equal(original.SentAtUtc, preserved.SentAtUtc);
            Assert.Equal(1, preserved.FailedAttempts);
            Assert.Null(preserved.InvalidatedAtUtc);
            Assert.Null(preserved.ConsumedAtUtc);
            Assert.Equal(emailOutbox, await db.OutboxMessages.AsNoTracking().Where(row => row.Channel == "email")
                .Select(row => row.Id).OrderBy(id => id).ToArrayAsync(Ct));
            await RecorridoEmailPump.ProcessOnceAsync(app.Services, Ct);
            Assert.Empty(Directory.GetFiles(directory, "*.eml"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    public async Task External_host_preserves_google_signup_challenge_without_inheriting_development_credentials(string access)
    {
        var directory = Workspace();
        try
        {
            await using var external = ApiFactory.ForIsolatedDatabase(Connections());
            await external.InitializeDatabaseAsync(Ct);
            await using var app = RecorridoApiHost.Configure(external, Root(), directory, RecorridoWhatsAppModule.Configure);
            using var client = Client(app);
            using var tokenResponse = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
            Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
            var token = (await JsonAsync(tokenResponse)).GetProperty("requestToken").GetString()!;
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["signup"] = "true", ["acceptedTerms"] = "true", ["access"] = access,
                ["returnTo"] = "/", ["__RequestVerificationToken"] = token,
            });
            using var response = await client.PostAsync("/api/auth/external/google", form, Ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("accounts.google.com", response.Headers.Location!.Host);
            var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(response.Headers.Location.Query);
            Assert.Equal("e2e-synthetic-client.apps.googleusercontent.com", parameters["client_id"].ToString());
            Assert.Equal("https://localtest.me:5174/signin-google", parameters["redirect_uri"].ToString());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task External_database_host_verifies_signup_email_and_legacy_cookie_and_persists_signed_bot_without_whatsapp_delivery()
    {
        var directory = Workspace();
        try
        {
            await using var external = ApiFactory.ForIsolatedDatabase(Connections());
            await external.InitializeDatabaseAsync(Ct);
            await using var app = RecorridoApiHost.Configure(external, Root(), directory, RecorridoWhatsAppModule.Configure);
            using var client = Client(app);
            await app.Services.SeedDatabaseAsync(Ct);
            Assert.Equal(postgres.RuntimeConnectionString, app.Services.GetRequiredService<IConfiguration>().GetConnectionString("appdb"));
            Assert.True(app.Services.GetRequiredService<IHostEnvironment>().IsEnvironment("Testing"));
            Assert.DoesNotContain(app.Services.GetRequiredService<Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartManager>().ApplicationParts,
                part => part is TestControllerApplicationPart);
            using (var probe = await client.PostAsync("/test/whatsapp-quota/fixture-number", new StringContent("{}"), Ct))
                Assert.Equal(HttpStatusCode.NotFound, probe.StatusCode);
            Assert.DoesNotContain(app.Services.GetServices<IHostedService>(), service =>
                service.GetType().Name is "AccountRecoveryNoticeWorker" or "OutboxDispatcher");
            var ready = Path.Combine(directory, "mt-e2e-test.ready");
            var bridge = new WhatsAppFileBridge(app.Services, ready, isolated: true);
            var begin = new WhatsAppBrowserRequest("begin", "walkthrough-signup", "+5491134567850");
            await CommandAsync(bridge, ready, client, begin);
            using (var requested = await AccountJourney.PostAsync(client, "/api/auth/signup",
                new { phone = Phone(begin.Phone), channel = "whatsapp", acceptedTerms = true }, Ct))
                Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var reply = await CommandAsync(bridge, ready, client, begin with { Kind = "code" });
            using (var verified = await AccountJourney.PostAsync(client, "/api/auth/signup/verify",
                new { phone = Phone(begin.Phone), channel = "whatsapp", acceptedTerms = true, code = reply.GetProperty("proof").GetString() }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
            await AccountJourney.AuthorizeAsync(client, "consumer", Ct, redirectUri: Redirect);
            const string email = "unified-backup@example.test";
            Guid methodId;
            using (var added = await AccountJourney.PostAsync(client, "/api/me/login-methods", new { email }, Ct))
            {
                Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
                methodId = (await JsonAsync(added)).GetProperty("methodId").GetGuid();
            }
            var emailCode = await PickupAsync(app.Services, directory, email);
            using (var verified = await AccountJourney.PostAsync(client, $"/api/me/login-methods/{methodId:D}/verify", new { code = emailCode }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
            client.DefaultRequestHeaders.Authorization = null;
            var bot = begin with { Scenario = "walkthrough-bot", Phone = "+5491134567851" };
            await CommandAsync(bridge, ready, client, bot);
            Assert.Equal(200, (await CommandAsync(bridge, ready, client, bot with { Kind = "inbound" })).GetProperty("status").GetInt32());
            Assert.Equal(200, (await CommandAsync(bridge, ready, client, bot with { Kind = "inbound" })).GetProperty("status").GetInt32());
            var link = await CommandAsync(bridge, ready, client, bot with { Kind = "bot" });
            Assert.Equal("/registro", new Uri(link.GetProperty("proof").GetString()!).AbsolutePath);
            var events = (await CommandAsync(bridge, ready, client, bot with { Kind = "events" })).GetProperty("events");
            var persisted = Assert.Single(events.EnumerateArray());
            Assert.True(persisted.GetProperty("processed").GetBoolean());
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var provider = scope.ServiceProvider;
                var db = provider.GetRequiredService<ApplicationDbContext>();
                Assert.All(await db.OutboxMessages.AsNoTracking().Where(row => row.Channel == "whatsapp").ToArrayAsync(Ct), row =>
                {
                    Assert.Equal(0, row.Attempts);
                    Assert.Null(row.SentAtUtc);
                    Assert.NotEmpty(row.EncryptedPayload);
                });
                Assert.Equal(1, await db.Set<WhatsAppMessage>().CountAsync(row => row.Wamid == "wamid.fixture.walkthrough-bot", Ct));
                var sender = provider.GetServices<IChannelSender>().Single(value => value.Key == "whatsapp");
                await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("synthetic", Ct));
            }
            await LegacyCookieAsync(app);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task External_host_notice_is_explicit_preserves_original_pending_version_and_repeats_before_one_http_approval()
    {
        var directory = Workspace();
        try
        {
            await using var external = ApiFactory.ForIsolatedDatabase(Connections());
            await external.InitializeDatabaseAsync(Ct);
            await using var app = RecorridoApiHost.Configure(external, Root(), directory, RecorridoWhatsAppModule.Configure);
            using var owner = Client(app);
            using var anonymous = Client(app);
            using var platform = Client(app);
            await app.Services.SeedDatabaseAsync(Ct);
            var ready = Path.Combine(directory, "mt-e2e-notice.ready");
            var bridge = new WhatsAppFileBridge(app.Services, ready, isolated: true);
            var original = new WhatsAppBrowserRequest("begin", "walkthrough-recovery-owner", "+5491134567852");
            await CommandAsync(bridge, ready, owner, original);
            using (var requested = await AccountJourney.PostAsync(owner, "/api/auth/signup", new { phone = Phone(original.Phone), channel = "whatsapp", acceptedTerms = true }, Ct))
                Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var proof = await CommandAsync(bridge, ready, owner, original with { Kind = "code" });
            using (var verified = await AccountJourney.PostAsync(owner, "/api/auth/signup/verify", new { phone = Phone(original.Phone), channel = "whatsapp", acceptedTerms = true, code = proof.GetProperty("proof").GetString() }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
            const string operatorEmail = "e2e-operator@example.test";
            using (var requested = await AccountJourney.PostAsync(platform, "/api/auth/login-code", new { email = operatorEmail }, Ct))
                Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var code = await PickupAsync(app.Services, directory, operatorEmail);
            using (var verified = await AccountJourney.PostAsync(platform, "/api/auth/login-code/verify", new { email = operatorEmail, code, returnUrl = "/connect/authorize?access=platform" }, Ct))
                Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            await AccountJourney.AuthorizeAsync(platform, "platform", Ct, redirectUri: Redirect);
            using (var me = await platform.GetAsync("/api/me", Ct))
            {
                var docs = (await JsonAsync(me)).GetProperty("pendingLegalDocuments").EnumerateArray()
                    .Select(document => new { id = document.GetProperty("id").GetGuid(), version = document.GetProperty("version").GetInt32() }).ToArray();
                using var accepted = await AccountJourney.PostAsync(platform, "/api/legal/accept", new { documents = docs }, Ct);
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            }
            var begin = original with { Scenario = "walkthrough-recovery", Phone = "+5491134567853" };
            await CommandAsync(bridge, ready, anonymous, begin);
            var nonce = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            using (var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code", new { phone = Phone(begin.Phone), channel = "whatsapp", nonce }, Ct))
                Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var recoveryProof = await CommandAsync(bridge, ready, anonymous, begin with { Kind = "code" });
            using (var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Phone(begin.Phone), channel = "whatsapp", nonce, code = recoveryProof.GetProperty("proof").GetString() }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
            using (var submitted = await AccountJourney.PostAsync(anonymous, "/api/account-recovery", new { phone = Phone(begin.Phone), channel = "whatsapp", nonce, previousPhone = Phone(original.Phone) }, Ct))
                Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
            using var listed = await platform.GetAsync("/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(begin.Phone), Ct);
            Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
            var row = Assert.Single((await JsonAsync(listed)).GetProperty("items").EnumerateArray());
            var id = row.GetProperty("id").GetGuid();
            var version = row.GetProperty("version").GetUInt32();
            await RecorridoEmailPump.ProcessOnceAsync(app.Services, Ct);
            await using (var scope = app.Services.CreateAsyncScope())
                Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryNotices.AnyAsync(value => value.Id == id, Ct));
            // La orden conserva el cursor abierto antes del pedido HTTP.
            var notice = begin with { Kind = "recoveryNotice", RecoveryId = id };
            var reply = await CommandAsync(bridge, ready, anonymous, notice);
            Assert.True(reply.GetProperty("marked").GetBoolean());
            Assert.True(reply.GetProperty("queuedNotices").GetInt32() > 0);
            Assert.Equal(version, reply.GetProperty("beforeVersion").GetUInt32());
            Assert.Equal(version, reply.GetProperty("afterVersion").GetUInt32());
            var repeated = await CommandAsync(bridge, ready, anonymous, notice);
            Assert.Equal(0, repeated.GetProperty("queuedNotices").GetInt32());
            Assert.Equal(version, repeated.GetProperty("afterVersion").GetUInt32());
            using var approved = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve", new { version, reason = "Aviso explícito del entorno unificado" }, Ct);
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            Assert.False(await bridge.TryExecuteAsync(anonymous, Ct));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private IsolatedDatabaseConnections Connections() => new(postgres.BootstrapConnectionString, postgres.AdminConnectionString, postgres.RuntimeConnectionString);
    private static object Phone(string number) => new { country = "AR", number };
    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new() { BaseAddress = new Uri("https://localtest.me:5174"), AllowAutoRedirect = false });
    private static string Workspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mt-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ArquitecturaBaseMultitenant.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return document!.RootElement.Clone();
    }
    private static async Task<JsonElement> CommandAsync(WhatsAppFileBridge bridge, string ready, HttpClient client, WhatsAppBrowserRequest request)
    {
        var file = Path.ChangeExtension(ready, ".whatsapp.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(request, JsonOptions), Ct);
        try
        {
            Assert.True(await bridge.TryExecuteAsync(client, Ct));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file + ".done", Ct));
            return document.RootElement.Clone();
        }
        finally { File.Delete(file); File.Delete(file + ".done"); }
    }
    private static async Task<string> PickupAsync(IServiceProvider services, string directory, string email)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await RecorridoEmailPump.ProcessOnceAsync(services, Ct);
            foreach (var file in Directory.GetFiles(directory, "*.eml"))
            {
                using var message = await MimeKit.MimeMessage.LoadAsync(file, Ct);
                if (!message.To.Mailboxes.Any(mailbox => mailbox.Address == email)) continue;
                var match = System.Text.RegularExpressions.Regex.Match(message.TextBody ?? "", @"(?m)^(?<code>\d{6})\r?$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                if (match.Success) { File.Delete(file); return match.Groups["code"].Value; }
            }
        }
        throw new InvalidOperationException("Controlled pickup OTP missing.");
    }
    private static async Task LegacyCookieAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("e2e-operator@example.test");
        Assert.NotNull(user);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        var started = services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(-2);
        var properties = new AuthenticationProperties { IsPersistent = true, IssuedUtc = started, ExpiresUtc = started.AddDays(30) };
        properties.Items[BrowserSessionKeys.CookieSessionId] = Guid.NewGuid().ToString("N");
        properties.Items[BrowserSessionKeys.StartedAtUtc] = BrowserSessionKeys.FormatStartedAtUtc(started.UtcDateTime);
        var cookie = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        using var browser = Client(app);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/connect/authorize?client_id=web&response_type=code&redirect_uri=https%3A%2F%2Flocaltest.me%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20api&code_challenge=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA&code_challenge_method=S256&access=consumer");
        request.Headers.Add("Cookie", cookie.Cookie.Name + "=" + cookie.TicketDataFormat.Protect(new AuthenticationTicket(principal, properties, IdentityConstants.ApplicationScheme)));
        using var response = await browser.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login?returnUrl=", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application=;", StringComparison.Ordinal));
    }
}
