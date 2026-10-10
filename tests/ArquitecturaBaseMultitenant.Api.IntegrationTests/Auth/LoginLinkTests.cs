using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

public sealed partial class LoginLinkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    private WebApplicationFactory<Program> Host(bool enabled = true) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureTestServices(services =>
        {
            if (enabled) services.AddSingleton<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging.ILoginCodeChannel, LoginLinkPhoneChannel>();
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(_clock);
            // No external transport/background processing in these database and HTTP security tests.
            services.RemoveAll<IHostedService>();
        });
    });
    [Fact]
    public async Task Preview_is_read_only_and_consumer_redeem_replays_body_without_cookie()
    {
        await using var app = Host();
        using var accountClient = app.CreateClient(new() { AllowAutoRedirect = false });
        var (user, method, token) = await PrepareAsync(app, accountClient);
        using var anonymous = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        for (var i = 0; i < 2; i++)
        {
            using var preview = await PostAsync(anonymous, "preview", new { token });
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode); Assert.False(preview.Headers.Contains("Set-Cookie"));
            var body = await preview.Content.ReadFromJsonAsync<JsonObject>(Ct);
            Assert.DoesNotContain("+549", body!.ToJsonString());
        }
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Null((await db.Set<LoginLink>().SingleAsync(value => value.MethodId == method, Ct)).ConsumedAtUtc);
            Assert.Equal(0, await db.LoginAudits.CountAsync(value => value.UserId == user && value.Method == LoginAuditMethod.WhatsAppLink, Ct));
        }
        var key = Guid.NewGuid();
        using var redeem = await PostAsync(anonymous, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        Assert.Contains(redeem.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Application", StringComparison.Ordinal));
        Assert.DoesNotContain(redeem.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Platform", StringComparison.Ordinal));
        using var replay = await PostAsync(anonymous, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        Assert.False(replay.Headers.Contains("Set-Cookie"));
        Assert.Equal(await redeem.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var different = await PostAsync(anonymous, "redeem", new { token, access = "consumer" });
        await InvalidAsync(different);
        using var spentPreview = await PostAsync(anonymous, "preview", new { token }); await InvalidAsync(spentPreview);
    }
    [Theory]
    [InlineData("removed")]
    [InlineData("reassigned")]
    [InlineData("operator")]
    [InlineData("suspended")]
    public async Task Replay_revalidates_current_account_and_original_method(string change)
    {
        await using var app = Host(); using var registered = app.CreateClient();
        var (user, method, token) = await PrepareAsync(app, registered);
        using var client = app.CreateClient(new() { HandleCookies = false });
        var key = Guid.NewGuid();
        using var first = await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var users = provider.GetRequiredService<IUserRepository>();
            var methods = provider.GetRequiredService<ILoginMethodRepository>();
            await methods.LockUserAsync(user, ct);
            if (change == "operator") await users.SetPlatformOperatorAsync(user, true, ct);
            else if (change == "suspended") Assert.True((await users.SuspendAsync(user, ct)).IsSuccess);
            else
            {
                var row = await methods.GetByIdForUserAsync(user, method, ct);
                methods.Remove(row!);
            }
        });
        if (change == "reassigned") await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var next = await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "UTC", ct);
            var phone = LoginMethod.CreatePhone(next.Id, PhoneNumber.Create("+5491134567890").Value);
            phone.Verify(_clock.GetUtcNow().UtcDateTime); provider.GetRequiredService<ILoginMethodRepository>().Add(phone);
        });
        using var replay = await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
        await InvalidAsync(replay);
    }
    [Fact]
    public async Task Operator_conversion_and_disabled_channel_reject_unconsumed_preview_redeem_and_replay()
    {
        await using var enabled = Host(); using var registered = enabled.CreateClient();
        var (user, _, token) = await PrepareAsync(enabled, registered);
        await TransactionAsync(enabled.Services, async (provider, ct) =>
        { await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct); await provider.GetRequiredService<IUserRepository>().SetPlatformOperatorAsync(user, true, ct); });
        using var client = enabled.CreateClient(new() { HandleCookies = false });
        using var preview = await PostAsync(client, "preview", new { token }); await InvalidAsync(preview);
        using var redeem = await PostAsync(client, "redeem", new { token, access = "consumer" }); await InvalidAsync(redeem);
        var (other, _, disabledToken) = await PrepareAsync(enabled, registered);
        await using var disabled = Host(false); using var disabledClient = disabled.CreateClient(new() { HandleCookies = false });
        using var off = await PostAsync(disabledClient, "preview", new { token = disabledToken }); await InvalidAsync(off);
        using var offRedeem = await PostAsync(disabledClient, "redeem", new { token = disabledToken, access = "consumer" }); await InvalidAsync(offRedeem);
        Assert.NotEqual(user, other);
    }
    [Fact]
    public async Task Two_concurrent_redemptions_consume_once_and_cooldown_supersedes_old_proof()
    {
        await using var app = Host(); using var registered = app.CreateClient();
        var (user, method, token) = await PrepareAsync(app, registered);
        using var first = app.CreateClient(new() { HandleCookies = false }); using var second = app.CreateClient(new() { HandleCookies = false });
        var responses = await Task.WhenAll(PostAsync(first, "redeem", new { token, access = "consumer" }), PostAsync(second, "redeem", new { token, access = "consumer" }));
        Assert.Single(responses, value => value.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, value => value.StatusCode == HttpStatusCode.BadRequest);
        foreach (var response in responses) response.Dispose();
        var tooSoon = await IssueAsync(app.Services, user, method);
        Assert.Equal(LoginLinkErrors.TooManyRequestsCode, tooSoon.Error.Code);
        _clock.Advance(TimeSpan.FromSeconds(61));
        var next = await IssueAsync(app.Services, user, method); Assert.True(next.IsSuccess);
        _clock.Advance(TimeSpan.FromSeconds(61));
        var newer = await IssueAsync(app.Services, user, method); Assert.True(newer.IsSuccess);
        using var superseded = await PostAsync(first, "preview", new { token = next.Value.Token }); await InvalidAsync(superseded);
        _clock.Advance(TimeSpan.FromMinutes(10));
        using var expired = await PostAsync(first, "redeem", new { token = newer.Value.Token, access = "consumer" }); await InvalidAsync(expired);
    }
    [Fact]
    public async Task Redeem_cookie_allows_current_terms_acceptance_before_protected_API()
    {
        await using var app = Host(); using var client = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var (_, _, token) = await PrepareAsync(app, client);
        using var redeem = await PostAsync(client, "redeem", new { token, access = "consumer" });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using var blocked = await client.GetAsync("/api/me/login-methods", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("Legal.AcceptanceRequired", (await blocked.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>());
        using var me = await client.GetAsync("/api/me", Ct); Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var snapshot = await me.Content.ReadFromJsonAsync<JsonObject>(Ct);
        var documents = snapshot!["pendingLegalDocuments"]!.AsArray().Select(value => new
        { id = value!["id"]!.GetValue<Guid>(), version = value["version"]!.GetValue<int>() }).ToArray();
        Assert.NotEmpty(documents);
        using var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using var allowed = await client.GetAsync("/api/me/login-methods", Ct); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
    [Fact]
    public async Task Proof_expiring_while_waiting_for_account_lock_is_rejected_with_fresh_clock()
    {
        await using var app = Host(); using var setup = app.CreateClient(); var (user, _, token) = await PrepareAsync(app, setup);
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = TransactionAsync(app.Services, async (provider, ct) =>
        { await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct); locked.SetResult(); await release.Task.WaitAsync(ct); });
        await locked.Task.WaitAsync(Ct);
        using var client = app.CreateClient(new() { HandleCookies = false }); var redeem = PostAsync(client, "redeem", new { token, access = "consumer" });
        try
        {
            await using var read = app.Services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            while (await db.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted""").SingleAsync(timeout.Token) == 0)
                await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            Assert.False(redeem.IsCompleted); _clock.Advance(TimeSpan.FromMinutes(10));
        }
        finally { release.TrySetResult(); await holding; }
        using var response = await redeem; await InvalidAsync(response);
    }
    [Fact]
    public async Task Five_proofs_per_window_are_persistent_and_access_never_selects_business_or_platform()
    {
        await using var app = Host(); using var setup = app.CreateClient(); var (user, method, token) = await PrepareAsync(app, setup);
        using var client = app.CreateClient(new() { HandleCookies = false });
        foreach (var access in new[] { "business", "platform" })
        {
            using var rejected = await PostAsync(client, "redeem", new { token, access });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode); Assert.False(rejected.Headers.Contains("Set-Cookie"));
        }
        for (var count = 1; count < 5; count++)
        { _clock.Advance(TimeSpan.FromSeconds(61)); Assert.True((await IssueAsync(app.Services, user, method)).IsSuccess); }
        _clock.Advance(TimeSpan.FromSeconds(61)); var sixth = await IssueAsync(app.Services, user, method);
        Assert.Equal(LoginLinkErrors.TooManyRequestsCode, sixth.Error.Code);
        _clock.Advance(TimeSpan.FromMinutes(15)); Assert.True((await IssueAsync(app.Services, user, method)).IsSuccess);
    }
    [Fact]
    public async Task Locked_account_with_verified_phone_gets_no_cookie()
    {
        await using var app = Host(); using var setup = app.CreateClient(); var (user, _, token) = await PrepareAsync(app, setup);
        using var client = app.CreateClient(new() { HandleCookies = false });
        using var preview = await PostAsync(client, "preview", new { token }); Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            var account = await provider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(value => value.Id == user, ct);
            account.LockoutEnabled = true; account.LockoutEnd = _clock.GetUtcNow().AddMinutes(5);
        });
        using var locked = await PostAsync(client, "redeem", new { token, access = "consumer" });
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode); Assert.False(locked.Headers.Contains("Set-Cookie"));
        Assert.Equal("Identity.Account.LockedOut", (await locked.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>());
    }
    [Theory]
    [InlineData("preview")]
    [InlineData("redeem")]
    [InlineData("replay")]
    public async Task Expired_lockout_uses_injected_clock_for_new_proof_and_cached_replay(string action)
    {
        // Identity's system clock remains before this lockout even after the injected clock advances.
        _clock.SetUtcNow(new DateTimeOffset(2100, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var app = Host(); using var setup = app.CreateClient();
        var (user, method, token) = await PrepareAsync(app, setup);
        using var client = app.CreateClient(new() { HandleCookies = false }); var key = Guid.NewGuid();
        if (action == "replay")
        {
            using var original = await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
            Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        }
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct);
            var account = await provider.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(value => value.Id == user, ct);
            account.LockoutEnabled = true; account.LockoutEnd = _clock.GetUtcNow().AddMinutes(5);
        });
        Assert.Equal(LoginLinkErrors.InvalidCode, (await IssueAsync(app.Services, user, method)).Error.Code);
        _clock.Advance(TimeSpan.FromMinutes(6));
        if (action != "replay")
        {
            var issued = await IssueAsync(app.Services, user, method); Assert.True(issued.IsSuccess);
            token = issued.Value.Token;
        }
        using var response = action == "preview"
            ? await PostAsync(client, "preview", new { token })
            : await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(action == "redeem", response.Headers.Contains("Set-Cookie"));
        Assert.Equal(action == "replay", response.Headers.Contains("Idempotent-Replayed"));
    }
    [Fact]
    public async Task Pending_deletion_cached_proof_repeats_only_while_original_account_is_eligible()
    {
        await using var app = Host(); using var setup = app.CreateClient(); var (user, _, token) = await PrepareAsync(app, setup);
        await TransactionAsync(app.Services, async (provider, ct) =>
        { await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct); Assert.True((await provider.GetRequiredService<IUserRepository>().RequestDeletionAsync(user, "fixture", _clock.GetUtcNow().UtcDateTime, 30, ct)).IsSuccess); });
        using var client = app.CreateClient(new() { HandleCookies = false }); var key = Guid.NewGuid();
        using var first = await PostAsync(client, "redeem", new { token, access = "consumer" }, key); Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync(Ct);
        using var replay = await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(first.StatusCode, replay.StatusCode); Assert.Equal(original, await replay.Content.ReadAsStringAsync(Ct)); Assert.False(replay.Headers.Contains("Set-Cookie"));
        await using (var read = app.Services.CreateAsyncScope())
            Assert.Equal(1, await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountDeletionCancelTickets.CountAsync(value => value.UserId == user, Ct));
        await TransactionAsync(app.Services, async (provider, ct) =>
        { await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct); await provider.GetRequiredService<IUserRepository>().SetPlatformOperatorAsync(user, true, ct); });
        using var converted = await PostAsync(client, "redeem", new { token, access = "consumer" }, key);
        Assert.Equal(HttpStatusCode.BadRequest, converted.StatusCode); Assert.False(converted.Headers.Contains("Set-Cookie"));
        var problem = await converted.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(LoginLinkErrors.InvalidCode, problem!["code"]!.GetValue<string>()); Assert.Null(problem["cancelTicket"]); Assert.Null(problem["returnUrl"]);
    }
    [Fact]
    public async Task Pending_deletion_uses_fixed_consumer_continuation_and_cancel_cookie()
    {
        await using var app = Host(); using var registered = app.CreateClient();
        var (user, _, token) = await PrepareAsync(app, registered);
        await TransactionAsync(app.Services, async (provider, ct) =>
        { await provider.GetRequiredService<ILoginMethodRepository>().LockUserAsync(user, ct); Assert.True((await provider.GetRequiredService<IUserRepository>().RequestDeletionAsync(user, "fixture", _clock.GetUtcNow().UtcDateTime, 30, ct)).IsSuccess); });
        using var client = app.CreateClient(new() { HandleCookies = false });
        using var redeem = await PostAsync(client, "redeem", new { token, access = "consumer" });
        Assert.Equal(HttpStatusCode.Forbidden, redeem.StatusCode);
        var problem = await redeem.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal("/login/enlace", problem!["returnUrl"]!.GetValue<string>());
        var ticket = problem["cancelTicket"]!.GetValue<string>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/deletion/cancel") { Content = JsonContent.Create(new { cancelTicket = ticket }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var cancel = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Contains(cancel.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Application", StringComparison.Ordinal));
        Assert.DoesNotContain(cancel.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Platform", StringComparison.Ordinal));
    }
    private async Task<(Guid User, Guid Method, string Token)> PrepareAsync(WebApplicationFactory<Program> app, HttpClient client)
    {
        await app.Services.SeedDatabaseAsync(Ct);
        var user = Guid.Empty;
        await TransactionAsync(app.Services, async (provider, ct) =>
        { user = (await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id; });
        var method = Guid.Empty;
        await TransactionAsync(app.Services, async (provider, ct) =>
        {
            var db = provider.GetRequiredService<ApplicationDbContext>();
            // This fixture number belongs to one account at a time; prior tests keep their independent link history.
            db.LoginMethods.RemoveRange(await db.LoginMethods.Where(value => value.Type == LoginMethodType.Phone && value.Value == "+5491134567890").ToArrayAsync(ct));
        });
        await TransactionAsync(app.Services, (provider, _) =>
        {
            var phone = LoginMethod.CreatePhone(user, PhoneNumber.Create("+5491134567890").Value);
            phone.Verify(_clock.GetUtcNow().UtcDateTime); method = phone.Id;
            provider.GetRequiredService<ILoginMethodRepository>().Add(phone); return Task.CompletedTask;
        });
        var result = await IssueAsync(app.Services, user, method); Assert.True(result.IsSuccess);
        return (user, method, result.Value.Token);
    }
    private static async Task<Result<IssuedLoginLink>> IssueAsync(IServiceProvider services, Guid user, Guid method)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            ct => scope.ServiceProvider.GetRequiredService<ILoginLinkIssuer>().IssueAsync(user, method, ct), CommitPolicy.OnSuccess, Ct);
    }
    private static async Task TransactionAsync(IServiceProvider services, Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string action, object body, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-link/" + action) { Content = JsonContent.Create(body) };
        if (action == "redeem") request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await client.SendAsync(request, Ct);
    }
    private static async Task InvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        Assert.Equal(LoginLinkErrors.InvalidCode, body!["code"]!.GetValue<string>());
        Assert.Null(body["displayName"]); Assert.Null(body["maskedPhone"]);
    }
}
