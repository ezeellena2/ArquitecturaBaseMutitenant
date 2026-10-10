using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>HTTP productivo y pruebas transaccionales de dos conexiones; transporte Meta no se ejecuta.</summary>
public sealed class WhatsAppQuotaEnqueueTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
    private readonly string _number = "quota-" + Guid.NewGuid().ToString("N");
    [Fact]
    public async Task HTTP_new_80_preserves_20_verified_logins_and_a_single_total_100()
    {
        await using var host = Host(); using var client = host.CreateClient();
        var phone = NewPhone(); await RegisterPhoneAsync(host, client, phone); _clock.Advance(TimeSpan.FromDays(1));
        for (var i = 0; i < 80; i++)
        {
            using var sent = await PostAsync(client, "/api/auth/signup", Signup(NewPhone()), "192.0.2." + (i / 20 + 1));
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        }
        using (var newDenied = await PostAsync(client, "/api/auth/signup", Signup(NewPhone()), "192.0.2.99"))
            await AssertDailyLimitAsync(newDenied);
        using (var purposeDenied = await PostAsync(client, "/api/auth/business-signup/code", Signup(phone), "192.0.2.100"))
            await AssertDailyLimitAsync(purposeDenied);
        for (var i = 0; i < 20; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(3));
            using var sent = await PostAsync(client, "/api/auth/login-code", Contact(phone), "192.0.2.101");
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        }
        _clock.Advance(TimeSpan.FromMinutes(3));
        using var denied = await PostAsync(client, "/api/auth/login-code", Contact(phone), "192.0.2.102");
        await AssertDailyLimitAsync(denied);
        await AssertCountsAsync(host.Services, 100, 80);
    }
    [Fact]
    public async Task HTTP_verified_can_use_100_without_new_sends_and_country_removal_does_not_block_login()
    {
        await using var setup = Host(); using var setupClient = setup.CreateClient();
        var phone = NewPhone(); await RegisterPhoneAsync(setup, setupClient, phone); _clock.Advance(TimeSpan.FromDays(1));
        await using var host = Host(country: "US"); using var client = host.CreateClient();
        for (var i = 0; i < 100; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(3));
            using var sent = await PostAsync(client, "/api/auth/login-code", Contact(phone), "198.51.100." + (i / 20 + 1));
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        }
        _clock.Advance(TimeSpan.FromMinutes(3));
        using var denied = await PostAsync(client, "/api/auth/login-code", Contact(phone), "198.51.100.99");
        await AssertDailyLimitAsync(denied); await AssertCountsAsync(host.Services, 100, 0);
    }
    [Fact]
    public async Task HTTP_IP_21_denies_without_outbox_and_replay_spends_once_changed_body_is_422()
    {
        await using var host = Host(); using var client = host.CreateClient(); await host.Services.SeedDatabaseAsync(Ct);
        var key = Guid.CreateVersion7(); var phone = NewPhone(); var body = Signup(phone);
        using (var first = await PostAsync(client, "/api/auth/signup", body, "203.0.113.1", key)) Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        using (var replay = await PostAsync(client, "/api/auth/signup", body, "203.0.113.1", key))
        { Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode); Assert.True(replay.Headers.Contains("Idempotent-Replayed")); }
        using (var changed = await PostAsync(client, "/api/auth/signup", Signup(NewPhone()), "203.0.113.2", key))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        for (var i = 1; i < 20; i++)
        { using var sent = await PostAsync(client, "/api/auth/signup", Signup(NewPhone()), "203.0.113.1"); Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode); }
        var extra = NewPhone(); using var denied = await PostAsync(client, "/api/auth/business-signup/code", Signup(extra), "::ffff:203.0.113.1");
        await AssertDailyLimitAsync(denied); await AssertCountsAsync(host.Services, 20, 20);
        await using var scope = host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.LoginCodes.AnyAsync(row => row.Destination == extra, Ct));
        var quota = await CurrentQuotaAsync(db); Assert.Equal(20, (await db.Set<WhatsAppCodeQuotaIp>().SingleAsync(row => row.QuotaId == quota.Id, Ct)).ReservedCount);
    }
    [Theory]
    [InlineData("locked", false)] [InlineData("deleted", false)] [InlineData("expiredOperator", false)]
    [InlineData("suspended", true)] [InlineData("pendingDeletion", true)]
    public async Task Actual_delivery_decision_controls_spend_and_preserves_unsent_history(string state, bool sends)
    {
        await using var host = Host(); using var client = host.CreateClient();
        var phone = NewPhone();
        var owner = state == "expiredOperator" ? await RegisterOperatorPhoneAsync(host, phone, 12) : await RegisterPhoneAsync(host, client, phone);
        _clock.Advance(TimeSpan.FromDays(1));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var p = scope.ServiceProvider;
            await p.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await p.GetRequiredService<ILoginMethodRepository>().LockUserAsync(owner, ct);
                var users = p.GetRequiredService<IUserRepository>();
                if (state == "locked") for (var i = 0; i < 10; i++) await p.GetRequiredService<ISignInService>().RegisterFailedAttemptAsync(owner, ct);
                if (state == "suspended") Assert.True((await users.SuspendAsync(owner, ct)).IsSuccess);
                if (state is "deleted" or "pendingDeletion")
                {
                    Assert.True((await users.RequestDeletionAsync(owner, "Fixture deletion", _clock.GetUtcNow().UtcDateTime.AddDays(-31), 30, ct)).IsSuccess);
                    if (state == "deleted") Assert.True((await users.CompleteDeletionAsync(owner, _clock.GetUtcNow().UtcDateTime, "Deleted", ct)).IsSuccess);
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var response = await PostAsync(client, "/api/auth/login-code", Contact(phone)); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var read = host.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var issued = await db.LoginCodes.AsNoTracking().SingleAsync(row => row.Destination == phone && row.Purpose == LoginCodePurpose.Login, Ct);
        Assert.Equal(sends, issued.SentAtUtc is not null);
        if (sends) await AssertCountsAsync(host.Services, 1, 0);
        else Assert.False(await db.Set<WhatsAppCodeQuota>().AnyAsync(row => row.PhoneNumberId == _number && row.DayUtc == DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), Ct));
    }
    [Fact]
    public async Task Tiny_cap_rejects_new_without_empty_ledger_but_admits_real_verified_operator()
    {
        var phone = NewPhone(); await using var setup = Host();
        await RegisterOperatorPhoneAsync(setup, phone, 72); _clock.Advance(TimeSpan.FromDays(1));
        await using var host = Host(cap: 1); using var client = host.CreateClient();
        using var rejected = await PostAsync(client, "/api/auth/signup", Signup(NewPhone())); await AssertDailyLimitAsync(rejected);
        await using (var scope = host.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppCodeQuota>().AnyAsync(row => row.PhoneNumberId == _number && row.DayUtc == DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), Ct));
        using var sent = await PostAsync(client, "/api/auth/login-code", Contact(phone)); Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        await AssertCountsAsync(host.Services, 1, 0);
    }
    [Fact]
    public async Task Pending_resend_quota_rejection_under_OnAnyResult_preserves_old_code_and_pending_method()
    {
        await using var host = Host(cap: 2); using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var account = await AccountJourney.RegisterAsync(host, client, Ct); var phone = NewPhone();
        using var add = await PostAsync(client, "/api/me/login-methods", Contact(phone)); Assert.Equal(HttpStatusCode.Accepted, add.StatusCode);
        var body = await add.Content.ReadFromJsonAsync<JsonElement>(Ct); var methodId = body.GetProperty("methodId").GetGuid();
        _clock.Advance(TimeSpan.FromSeconds(60));
        using var denied = await PostAsync(client, $"/api/me/login-methods/{methodId}/code", new { }); await AssertDailyLimitAsync(denied);
        await using var read = host.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var code = await db.LoginCodes.AsNoTracking().SingleAsync(row => row.Destination == phone, Ct);
        Assert.Null(code.InvalidatedAtUtc); Assert.NotNull(code.SentAtUtc);
        Assert.Null((await db.LoginMethods.AsNoTracking().SingleAsync(row => row.Id == methodId && row.UserId == account.UserId, Ct)).VerifiedAtUtc);
        await AssertCountsAsync(host.Services, 1, 1);
    }
    [Fact]
    public async Task Dispatcher_failure_after_actual_HTTP_enqueue_does_not_refund_quota()
    {
        await using var isolated = new ApiFactory(); await isolated.InitializeAsync();
        await using var host = Host(source: isolated); using var client = host.CreateClient();
        await host.Services.SeedDatabaseAsync(Ct);
        using var response = await PostAsync(client, "/api/auth/signup", Signup(NewPhone()));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await AssertCountsAsync(host.Services, 1, 1);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OutboxDeliveryProcessor>().ProcessOnceAsync([new FailedSender()], Ct));
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .SingleAsync(message => message.Channel == "whatsapp", Ct);
        Assert.Equal(OutboxStatus.Failed, row.Status); Assert.Equal(1, row.Attempts);
        await AssertCountsAsync(host.Services, 1, 1);
    }
    [Fact]
    public async Task Mixed_two_connections_are_atomic_and_IP_retention_after_midnight_never_refunds_global_counts()
    {
        await using var host = Host(); using var client = host.CreateClient();
        var phone = NewPhone(); var owner = await RegisterPhoneAsync(host, client, phone); _clock.Advance(TimeSpan.FromDays(1));
        Assert.True((await ReserveAndQueueAsync(0, false, abort: true)).IsFailure);
        await AssertMissingQuotaAsync();
        using var twoConnections = new SemaphoreSlim(2);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 120).Select(async i =>
        {
            await twoConnections.WaitAsync(Ct);
            try { return await ReserveAndQueueAsync(i, i % 3 == 2, abort: false); }
            finally { twoConnections.Release(); }
        }));
        Assert.Equal(100, attempts.Count(value => value.IsSuccess));
        await using var read = host.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var quota = await CurrentQuotaAsync(db); var total = quota.ReservedCount; var newCount = quota.NewReservedCount;
        Assert.Equal(attempts.Count(value => value.IsSuccess), total); Assert.InRange(newCount, 60, 80);
        Assert.All(await db.Set<WhatsAppCodeQuotaIp>().AsNoTracking().Where(row => row.QuotaId == quota.Id).ToArrayAsync(Ct), row => Assert.InRange(row.ReservedCount, 1, 20));
        await CleanIpsAsync(); Assert.True(await db.Set<WhatsAppCodeQuotaIp>().AnyAsync(row => row.QuotaId == quota.Id, Ct));
        _clock.Advance(TimeSpan.FromDays(1)); await CleanIpsAsync(); Assert.False(await db.Set<WhatsAppCodeQuotaIp>().AnyAsync(row => row.QuotaId == quota.Id, Ct));
        var preserved = await db.Set<WhatsAppCodeQuota>().AsNoTracking().SingleAsync(row => row.Id == quota.Id, Ct);
        Assert.Equal(total, preserved.ReservedCount); Assert.Equal(newCount, preserved.NewReservedCount);

        async Task<Result> ReserveAndQueueAsync(int index, bool protectedLogin, bool abort)
        {
            await using var scope = host.Services.CreateAsyncScope(); var p = scope.ServiceProvider;
            return await p.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var destination = LoginCodeDestination.ForPhone(PhoneNumber.Create(protectedLogin ? phone : NewPhone()).Value, "whatsapp");
                await p.GetRequiredService<LoginMethodLinker>().LockDestinationAsync(destination, ct);
                if (protectedLogin) await p.GetRequiredService<ILoginMethodRepository>().LockUserAsync(owner, ct);
                await p.GetRequiredService<ILoginCodeRepository>().LockDestinationAsync(destination, ct);
                var admission = new WhatsAppCodeQuotaIssuer(p.GetRequiredService<IWhatsAppCodeQuotaRepository>(), p.GetRequiredService<IWhatsAppChannelSettings>(), _clock,
                    p.GetRequiredService<ILoginMethodReader>(), p.GetRequiredService<LoginMethodAvailabilityPolicy>(), new Request("192.0.2." + (index / 20 + 1)), p.GetRequiredService<IWhatsAppQuotaIpHasher>());
                var result = await admission.ReserveAsync(destination, protectedLogin ? LoginCodePurpose.Login : LoginCodePurpose.BusinessSignup, owner, ct);
                if (result.IsFailure) return result;
                // Real rendering/encrypted enqueue in a technical transaction fixture; no Meta send or proof claim.
                new WhatsAppLoginCodeChannel(p.GetRequiredService<IWhatsAppChannelSettings>(), p.GetRequiredService<IWhatsAppTemplateRenderer>(), p.GetRequiredService<IOutbox>())
                    .EnqueueVerification(owner, destination.Value, "123456", 10,
                    await p.GetRequiredService<UserCultureResolver>().ResolveAsync(null, null, ct));
                return abort ? Result.Failure(Error.Conflict("Test.Transaction.Abort", "Fixture rollback.")) : result;
            }, CommitPolicy.OnSuccess, Ct);
        }
        async Task AssertMissingQuotaAsync()
        {
            await using var scope = host.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<WhatsAppCodeQuota>()
                .AnyAsync(row => row.PhoneNumberId == _number && row.DayUtc == DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), Ct));
        }
        async Task CleanIpsAsync()
        {
            await using var scope = host.Services.CreateAsyncScope(); var p = scope.ServiceProvider;
            await p.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var repo = p.GetRequiredService<IWhatsAppCodeQuotaRepository>();
                foreach (var ip in await repo.ListExpiredIpsAsync(DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), 100, ct)) repo.Remove(ip);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
    }
    private WebApplicationFactory<Program> Host(string country = "AR", int cap = 100, ApiFactory? source = null) => (source ?? factory).WithWebHostBuilder(builder =>
    {
        builder.UseSetting("WhatsApp:PhoneNumberId", _number).UseSetting("WhatsApp:AccessToken", "fixture-token")
            .UseSetting("WhatsApp:AllowedCountries:0", country).UseSetting("WhatsApp:DailyAuthCodeLimit", cap.ToString(System.Globalization.CultureInfo.InvariantCulture))
            // Isolate daily quotas; the independent rate-limit tests retain the productive 20/15-minute window.
            .UseSetting("RateLimiting:LoginCodePermitLimit", "100");
        builder.ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock); services.AddSingleton<IStartupFilter, RemoteIpFilter>(); });
    });
    private static async Task<Guid> RegisterPhoneAsync(WebApplicationFactory<Program> host, HttpClient client, string phone)
    {
        await host.Services.SeedDatabaseAsync(Ct);
        using var requested = await PostAsync(client, "/api/auth/signup", Signup(phone), "192.0.2.250"); Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await WhatsAppIdentityTests.CodeAsync(host.Services, phone);
        using var verified = await PostAsync(client, "/api/auth/signup/verify", Signup(phone, code), "192.0.2.250"); Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await using var scope = host.Services.CreateAsyncScope(); return (await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUserIdAsync(LoginMethodType.Phone, phone, Ct))!.Value;
    }
    private async Task<Guid> RegisterOperatorPhoneAsync(WebApplicationFactory<Program> host, string phone, int expiryHours)
    {
        await host.Services.SeedDatabaseAsync(Ct);
        await using var scope = host.Services.CreateAsyncScope(); var p = scope.ServiceProvider; var unit = p.GetRequiredService<IUnitOfWork>();
        Guid owner = Guid.Empty;
        var destination = LoginCodeDestination.ForPhone(PhoneNumber.Create(phone).Value, "whatsapp");
        var added = await unit.ExecuteInTransactionAsync(async ct =>
        {
            owner = (await p.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            await p.GetRequiredService<IUserRepository>().InvitePlatformOperatorAsync(owner, _clock.GetUtcNow().UtcDateTime.AddHours(expiryHours), ct);
            return await p.GetRequiredService<LoginMethodIssuer>().AddAsync(owner, destination, ct);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(added.IsSuccess);
        var code = await WhatsAppIdentityTests.CodeAsync(host.Services, phone);
        var verified = await unit.ExecuteInTransactionAsync(ct => p.GetRequiredService<LoginMethodVerifier>().VerifyAsync(owner,
            new VerifyLoginMethodRequest(added.Value.MethodId, code), ct), CommitPolicy.OnAnyResult, Ct);
        Assert.True(verified.IsSuccess);
        Assert.False(await p.GetRequiredService<ApplicationDbContext>().UserTenantAccesses.AnyAsync(row => row.UserId == owner, Ct));
        return owner;
    }
    private async Task<WhatsAppCodeQuota> CurrentQuotaAsync(ApplicationDbContext db) => await db.Set<WhatsAppCodeQuota>().AsNoTracking().SingleAsync(
        row => row.PhoneNumberId == _number && row.DayUtc == DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), Ct);
    private async Task AssertCountsAsync(IServiceProvider services, int total, int newCount)
    { await using var scope = services.CreateAsyncScope(); var row = await CurrentQuotaAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()); Assert.Equal(total, row.ReservedCount); Assert.Equal(newCount, row.NewReservedCount); }
    private static async Task AssertDailyLimitAsync(HttpResponseMessage response)
    { Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode); var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct); Assert.Equal(WhatsAppCodeErrors.DailyLimitCode, problem.GetProperty("code").GetString()); Assert.True(problem.GetProperty("retryAfter").GetInt32() > 0); }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string ip = "192.0.2.200", Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.CreateVersion7()).ToString()); request.Headers.Add("X-Test-Remote-IP", ip);
        return await client.SendAsync(request, Ct);
    }
    private static string NewPhone() => "+54911" + RandomNumberGenerator.GetInt32(30000000, 59999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static object Contact(string phone) => new { phone = new { country = "AR", number = phone }, channel = "whatsapp" };
    private static object Signup(string phone, string? code = null) => new { phone = new { country = "AR", number = phone }, channel = "whatsapp", acceptedTerms = true, code };
    private sealed class Request(string ip) : IRequestInfo { public string? IpAddress => ip; public string? UserAgent => null; }
    private sealed class FailedSender : IChannelSender
    {
        public string Key => "whatsapp";
        public Task SendAsync(string payload, CancellationToken ct) => throw new HttpRequestException("Fixture transport unavailable.");
    }
    private sealed class RemoteIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        { app.Use((context, following) => { if (IPAddress.TryParse(context.Request.Headers["X-Test-Remote-IP"].ToString(), out var address)) context.Connection.RemoteIpAddress = address; return following(context); }); next(app); };
    }
}
