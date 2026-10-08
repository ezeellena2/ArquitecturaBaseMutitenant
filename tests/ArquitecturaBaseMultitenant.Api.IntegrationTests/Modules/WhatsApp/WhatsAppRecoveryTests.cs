using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppRecoveryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Phone_recovery_preserves_cooldown_request_limit_and_failed_attempts()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using var app = Host().WithWebHostBuilder(builder => builder
            .UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "60")
            .UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "2")
            .UseSetting("Authentication:LoginCode:MaxAttempts", "2")
            .ConfigureTestServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        using var client = Client(app);
        var phone = NewPhone();
        var nonce = Nonce();
        var code = await RequestAsync(app, client, phone, nonce);
        using var cooldown = await PostAsync(client, "/api/account-recovery/code", new { phone = Input(phone), channel = "whatsapp", nonce });
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);
        Assert.Equal(LoginCodeErrors.ResendTooSoonCode, await ErrorAsync(cooldown));
        clock.Advance(TimeSpan.FromMinutes(1));
        code = await RequestAsync(app, client, phone, nonce);
        clock.Advance(TimeSpan.FromMinutes(1));
        using var limit = await PostAsync(client, "/api/account-recovery/code", new { phone = Input(phone), channel = "whatsapp", nonce });
        Assert.Equal(HttpStatusCode.TooManyRequests, limit.StatusCode);
        Assert.Equal(LoginCodeErrors.TooManyRequestsCode, await ErrorAsync(limit));
        using var crossedNonce = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce = Nonce(), code });
        Assert.Equal(LoginCodeErrors.InvalidCode, await ErrorAsync(crossedNonce));
        var wrongCode = (code[0] == '9' ? '0' : (char)(code[0] + 1)) + code[1..];
        using var wrong = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code = wrongCode });
        using var error = await wrong.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(1, error!.RootElement.GetProperty("attemptsLeft").GetInt32());
        using var exhausted = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code = wrongCode });
        Assert.Equal(LoginCodeErrors.TooManyAttemptsCode, await ErrorAsync(exhausted));
        using var blocked = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(LoginCodeErrors.TooManyAttemptsCode, await ErrorAsync(blocked));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var codes = await db.LoginCodes.Where(value => value.Destination == phone).ToArrayAsync(Ct);
        Assert.Equal(2, codes.Length);
        Assert.All(codes, value => Assert.Null(value.RequestedByUserId));
        Assert.Contains(codes, value => value.FailedAttempts == 2);
    }

    [Fact]
    public async Task Expired_phone_code_and_expired_verified_proof_cannot_create_a_recovery()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        await using var app = Host().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        using var client = Client(app);
        var phone = NewPhone();
        var nonce = Nonce();
        var code = await RequestAsync(app, client, phone, nonce);
        clock.Advance(TimeSpan.FromMinutes(11));
        using var expired = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(LoginCodeErrors.ExpiredCode, await ErrorAsync(expired));
        phone = NewPhone();
        nonce = Nonce();
        code = await RequestAsync(app, client, phone, nonce);
        using var verified = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var replay = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(LoginCodeErrors.AlreadyUsedCode, await ErrorAsync(replay));
        clock.Advance(TimeSpan.FromMinutes(11));
        using var submitted = await PostAsync(client, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousEmail = "lost@example.test" });
        Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode, await ErrorAsync(submitted));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AnyAsync(value => value.NewMethodValue == phone, Ct));
    }

    [Fact]
    public async Task Recovery_approves_phone_as_primary_keeps_methods_and_notifies_without_a_session()
    {
        await using var app = Host();
        using var owner = Client(app);
        var account = await AccountJourney.RegisterAsync(app, owner, Ct);
        var previous = NewPhone();
        await AddPhoneAsync(app.Services, account.UserId, previous);
        var phone = NewPhone();
        using var anonymous = Client(app);
        using var preview = await anonymous.PostAsJsonAsync("/api/account-recovery/preview", new { previousPhone = Input(previous) }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, preview.StatusCode);
        var nonce = Nonce();
        var code = await RequestAsync(app, anonymous, phone, nonce);
        using var verified = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var body = new { phone = Input(phone), channel = "whatsapp", nonce, previousPhone = Input(previous) };
        var key = Guid.CreateVersion7();
        var before = await OutboxIdsAsync(app.Services);
        using var submitted = await PostAsync(anonymous, "/api/account-recovery", body, key);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        Assert.False(submitted.Headers.Contains("Set-Cookie"));
        using var replay = await PostAsync(anonymous, "/api/account-recovery", body, key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        using var reused = await PostAsync(anonymous, "/api/account-recovery", body);
        Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode, await ErrorAsync(reused));
        Assert.Equal(new[] { "recuperacion_recibida" }, await TemplatesAsync(app.Services, phone, before));
        await ActivatorUtilities.CreateInstance<AccountRecoveryNoticeWorker>(app.Services).RunOnceAsync(Ct);
        Assert.Equal(new[] { "recuperacion_aviso_cuenta" }, await TemplatesAsync(app.Services, previous, before));

        using var platform = await OperatorAsync(app);
        var row = await RowAsync(platform, phone);
        Assert.Equal("Phone", row.GetProperty("previousMethodType").GetString());
        Assert.Equal("Phone", row.GetProperty("newMethodType").GetString());
        Assert.Equal("•••• " + phone[^4..], row.GetProperty("newMethod").GetString());
        before = await OutboxIdsAsync(app.Services);
        using var approved = await DecideAsync(platform, row, "approve");
        var version = await SavedVersion.ReadAsync(approved, Ct);
        Assert.False(approved.Headers.Contains("Set-Cookie"));
        Assert.Equal(new[] { "recuperacion_aprobada" }, await TemplatesAsync(app.Services, phone, before));
        Assert.Equal(new[] { "aviso_metodo_ingreso" }, await TemplatesAsync(app.Services, previous, before));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var methods = await db.LoginMethods.AsNoTracking().Where(value => value.UserId == account.UserId).ToArrayAsync(Ct);
        Assert.Equal(3, methods.Length);
        var primary = Assert.Single(methods, value => value.IsPrimary);
        Assert.Equal(phone, primary.Value);
        Assert.NotNull(primary.VerifiedAtUtc);
        var user = await db.Users.AsNoTracking().SingleAsync(value => value.Id == account.UserId, Ct);
        Assert.Null(user.Email);
        Assert.Equal(phone, user.PhoneNumber);
        Assert.Single(await db.AccountRecoveryRequests.Where(value => value.NewMethodValue == phone).ToArrayAsync(Ct));
        Assert.True(await db.SecurityEvents.AnyAsync(value => value.Type == SecurityEventType.AccountRecoveryApproved && value.TargetAccountId == account.UserId, Ct));
        Assert.True(await db.SecurityEvents.AnyAsync(value => value.Type == SecurityEventType.LoginMethodChanged && value.TargetAccountId == account.UserId, Ct));
        var resolved = await RowAsync(platform, phone);
        Assert.Equal(version, resolved.GetProperty("version").GetUInt32());
        using var stale = await DecideAsync(platform, row, "reject");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("General.ConcurrencyConflict", await ErrorAsync(stale));
        using var again = await DecideAsync(platform, resolved, "approve");
        Assert.Equal(AccountRecoveryErrors.AlreadyResolvedCode, await ErrorAsync(again));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Previous_phone_lookup_works_without_its_channel_or_allowed_country(bool disabled)
    {
        await using var original = Host();
        using var owner = Client(original);
        var account = await AccountJourney.RegisterAsync(original, owner, Ct);
        var previous = NewPhone();
        await AddPhoneAsync(original.Services, account.UserId, previous);
        await using var current = disabled ? factory.WithWebHostBuilder(builder => builder.UseSetting("WhatsApp:PhoneNumberId", "")) : Host("US");
        using var anonymous = Client(current);
        using var preview = await anonymous.PostAsJsonAsync("/api/account-recovery/preview", new { previousPhone = Input(previous) }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, preview.StatusCode);
        var email = "recovery-" + Guid.NewGuid().ToString("N") + "@example.test";
        var nonce = Nonce();
        using var requested = await PostAsync(anonymous, "/api/account-recovery/code", new { email, nonce });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(current.Services, email, Ct);
        using var verified = await PostAsync(anonymous, "/api/account-recovery/verify", new { email, nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await PostAsync(anonymous, "/api/account-recovery", new { email, nonce, previousPhone = Input(previous) });
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        Assert.False(submitted.Headers.Contains("Set-Cookie"));
        await using var scope = current.Services.CreateAsyncScope();
        Assert.Equal(account.UserId, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.SingleAsync(value => value.NewMethodValue == email, Ct)).UserId);
    }

    [Fact]
    public async Task Unknown_previous_phone_and_two_previous_methods_reject_without_writes()
    {
        await using var app = Host();
        using var anonymous = Client(app);
        var previous = NewPhone();
        using var preview = await anonymous.PostAsJsonAsync("/api/account-recovery/preview", new { previousPhone = Input(previous) }, Ct);
        await AssertFieldErrorAsync(preview, AccountRecoveryErrors.PhoneAccountNotFoundCode, "previousPhone");
        using var bothPreview = await anonymous.PostAsJsonAsync("/api/account-recovery/preview", new { previousPhone = Input(previous), previousEmail = "previous@example.test" }, Ct);
        await AssertFieldErrorAsync(bothPreview, "Validation.Failed", "previousPhone");
        var phone = NewPhone();
        var nonce = Nonce();
        var code = await RequestAsync(app, anonymous, phone, nonce);
        using var beforeProof = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousPhone = Input(previous) });
        Assert.Equal(AccountRecoveryErrors.VerificationRequiredCode, await ErrorAsync(beforeProof));
        using var verified = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        var before = await OutboxIdsAsync(app.Services);
        using var submitted = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousPhone = Input(previous) });
        await AssertFieldErrorAsync(submitted, AccountRecoveryErrors.PhoneAccountNotFoundCode, "previousPhone");
        using var repeated = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousPhone = Input(phone) });
        await AssertFieldErrorAsync(repeated, "Validation.Failed", "previousPhone");
        using var both = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousPhone = Input(previous), previousEmail = "previous@example.test" });
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        Assert.Empty(await TemplatesAsync(app.Services, phone, before));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AnyAsync(value => value.NewMethodValue == phone, Ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Occupied_phone_never_emits_code_or_false_notice_even_when_owned_by_the_requester(bool own)
    {
        await using var app = Host();
        using var owner = Client(app);
        var account = await AccountJourney.RegisterAsync(app, owner, Ct);
        var phone = NewPhone();
        await AddPhoneAsync(app.Services, account.UserId, phone);
        using var anonymous = Client(app);
        var before = await OutboxIdsAsync(app.Services);
        using var requested = await PostAsync(own ? owner : anonymous, "/api/account-recovery/code", new { phone = Input(phone), channel = "whatsapp", nonce = Nonce() });
        await AssertFieldErrorAsync(requested, AccountRecoveryErrors.PhoneMethodInUseCode, "phone");
        Assert.Empty(await TemplatesAsync(app.Services, phone, before));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AnyAsync(value => value.Destination == phone, Ct));
    }

    [Fact]
    public async Task Proof_is_bound_to_nonce_and_recovery_purpose_and_submit_rechecks_ownership()
    {
        await using var app = Host();
        using var owner = Client(app);
        var account = await AccountJourney.RegisterAsync(app, owner, Ct);
        var phone = NewPhone();
        using var anonymous = Client(app);
        var nonce = Nonce();
        var code = await RequestAsync(app, anonymous, phone, nonce);
        using var wrongNonce = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce = Nonce(), code });
        Assert.Equal(HttpStatusCode.BadRequest, wrongNonce.StatusCode);
        using var signup = await PostAsync(anonymous, "/api/auth/signup/verify", new { phone = Input(phone), channel = "whatsapp", code, acceptedTerms = true });
        Assert.Equal(HttpStatusCode.BadRequest, signup.StatusCode);
        using var verified = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await AddPhoneAsync(app.Services, account.UserId, phone);
        var before = await OutboxIdsAsync(app.Services);
        using var occupiedVerification = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        await AssertFieldErrorAsync(occupiedVerification, AccountRecoveryErrors.PhoneMethodInUseCode, "phone");
        using var submitted = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousEmail = account.Email.Value });
        await AssertFieldErrorAsync(submitted, AccountRecoveryErrors.PhoneMethodInUseCode, "phone");
        Assert.Empty(await TemplatesAsync(app.Services, phone, before));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountRecoveryRequests.AnyAsync(value => value.NewMethodValue == phone, Ct));
    }

    [Fact]
    public async Task Approval_reclaims_pending_phone_without_taking_a_verified_method()
    {
        await using var app = Host();
        using var person = Client(app);
        var account = await AccountJourney.RegisterAsync(app, person, Ct);
        using var other = Client(app);
        var otherAccount = await AccountJourney.RegisterAsync(app, other, Ct);
        using var anonymous = Client(app);
        var phone = NewPhone();
        using var pending = await PostAsync(other, "/api/me/login-methods", new { phone = Input(phone), channel = "whatsapp" });
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        await SubmitAsync(app, anonymous, phone, account.Email.Value);
        using var platform = await OperatorAsync(app);
        using var approved = await DecideAsync(platform, await RowAsync(platform, phone), "approve");
        await SavedVersion.ReadAsync(approved, Ct);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.LoginMethods.AnyAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone && value.UserId == otherAccount.UserId, Ct));
        Assert.Equal(account.UserId, (await db.LoginMethods.SingleAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone, Ct)).UserId);
    }

    [Fact]
    public async Task Competing_decisions_recheck_the_locked_version_and_apply_only_one_decision()
    {
        await using var app = Host();
        using var person = Client(app);
        var account = await AccountJourney.RegisterAsync(app, person, Ct);
        using var anonymous = Client(app);
        var phone = NewPhone();
        await SubmitAsync(app, anonymous, phone, account.Email.Value);
        using var platform = await OperatorAsync(app);
        var row = await RowAsync(platform, phone);
        var responses = await Task.WhenAll(DecideAsync(platform, row, "approve"), DecideAsync(platform, row, "reject"));
        try
        {
            Assert.Single(responses, value => value.IsSuccessStatusCode);
            var failed = Assert.Single(responses, value => !value.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
            Assert.Equal("General.ConcurrencyConflict", await ErrorAsync(failed));
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recovery = await db.AccountRecoveryRequests.SingleAsync(value => value.NewMethodValue == phone, Ct);
            Assert.NotEqual(AccountRecoveryStatus.Pending, recovery.Status);
            Assert.Equal(recovery.Status == AccountRecoveryStatus.Approved,
                await db.LoginMethods.AnyAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone, Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("submit")]
    [InlineData("approve")]
    public async Task Every_new_phone_phase_revalidates_actual_country(string phase)
    {
        await using var original = Host();
        using var owner = Client(original);
        var account = await AccountJourney.RegisterAsync(original, owner, Ct);
        using var anonymous = Client(original);
        var phone = NewPhone();
        var nonce = Nonce();
        var code = await RequestAsync(original, anonymous, phone, nonce);
        if (phase != "verify")
        {
            using var verified = await PostAsync(anonymous, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
            Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        }
        if (phase == "approve")
        {
            using var submitted = await PostAsync(anonymous, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousEmail = account.Email.Value });
            Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        }
        await using var restricted = Host("US");
        using var caller = phase == "approve" ? await OperatorAsync(restricted) : Client(restricted);
        using var rejected = phase == "approve" ? await DecideAsync(caller, await RowAsync(caller, phone), "approve")
            : await PostAsync(caller, phase == "verify" ? "/api/account-recovery/verify" : "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, code, previousEmail = account.Email.Value });
        await AssertFieldErrorAsync(rejected, "WhatsApp.Phone.CountryNotAllowed", "phone");
        await using var scope = restricted.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AnyAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone && value.VerifiedAtUtc != null, Ct));
    }

    [Fact]
    public async Task Rejection_and_operator_protection_keep_identity_unchanged()
    {
        await using var app = Host();
        using var target = await OperatorAsync(app);
        using var me = await target.GetAsync("/api/me", Ct);
        using var profile = await me.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var email = profile!.RootElement.GetProperty("email").GetString()!;
        using var anonymous = Client(app);
        var phone = NewPhone();
        await SubmitAsync(app, anonymous, phone, email);
        using var reviewer = await OperatorAsync(app);
        var row = await RowAsync(reviewer, phone);
        using var approved = await DecideAsync(reviewer, row, "approve");
        Assert.Equal(HttpStatusCode.Conflict, approved.StatusCode);
        Assert.Equal(AccountRecoveryErrors.OperatorAccountCode, await ErrorAsync(approved));
        var before = await OutboxIdsAsync(app.Services);
        using var rejected = await DecideAsync(reviewer, row, "reject");
        await SavedVersion.ReadAsync(rejected, Ct);
        Assert.Equal(new[] { "recuperacion_rechazada" }, await TemplatesAsync(app.Services, phone, before));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AnyAsync(value => value.Value == phone, Ct));
    }

    [Fact]
    public async Task Approval_and_registration_racing_for_the_same_phone_never_create_two_owners()
    {
        await using var app = Host();
        using var person = Client(app);
        var account = await AccountJourney.RegisterAsync(app, person, Ct);
        using var anonymous = Client(app);
        var phone = NewPhone();
        await SubmitAsync(app, anonymous, phone, account.Email.Value);
        using var platform = await OperatorAsync(app);
        var row = await RowAsync(platform, phone);
        using var signupClient = Client(app);
        using var signup = await PostAsync(signupClient, "/api/auth/signup", new { phone = Input(phone), channel = "whatsapp", acceptedTerms = true });
        Assert.Equal(HttpStatusCode.Accepted, signup.StatusCode);
        var code = await WhatsAppIdentityTests.CodeAsync(app.Services, phone);
        var responses = await Task.WhenAll(DecideAsync(platform, row, "approve"), PostAsync(signupClient, "/api/auth/signup/verify", new { phone = Input(phone), channel = "whatsapp", acceptedTerms = true, code }));
        try
        {
            Assert.Single(responses, value => value.IsSuccessStatusCode);
            var failed = Assert.Single(responses, value => !value.IsSuccessStatusCode);
            Assert.Contains(await ErrorAsync(failed), new[] { AccountRecoveryErrors.PhoneMethodInUseCode, "Auth.Signup.PhoneTaken" });
            await using var scope = app.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.CountAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone && value.VerifiedAtUtc != null, Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private WebApplicationFactory<Program> Host(string country = "AR") => factory.WithWebHostBuilder(builder => builder
        .UseSetting("WhatsApp:PhoneNumberId", "recovery-number").UseSetting("WhatsApp:AccessToken", "fixture-token")
        .UseSetting("WhatsApp:AppSecret", "fixture-secret").UseSetting("WhatsApp:VerifyToken", "fixture-verify")
        .UseSetting("WhatsApp:AllowedCountries:0", country).UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0")
        .UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "100"));
    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static string NewPhone() => "+54911" + RandomNumberGenerator.GetInt32(30000000, 59999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Nonce() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static object Input(string phone) => new { country = "US", number = phone };
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.CreateVersion7()).ToString());
        return await client.SendAsync(request, Ct);
    }
    private static async Task<string> RequestAsync(WebApplicationFactory<Program> app, HttpClient client, string phone, string nonce)
    {
        using var requested = await PostAsync(client, "/api/account-recovery/code", new { phone = Input(phone), channel = "whatsapp", nonce });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.False(requested.Headers.Contains("Set-Cookie"));
        return await WhatsAppIdentityTests.CodeAsync(app.Services, phone);
    }
    private static async Task SubmitAsync(WebApplicationFactory<Program> app, HttpClient client, string phone, string previousEmail)
    {
        var nonce = Nonce();
        var code = await RequestAsync(app, client, phone, nonce);
        using var verified = await PostAsync(client, "/api/account-recovery/verify", new { phone = Input(phone), channel = "whatsapp", nonce, code });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await PostAsync(client, "/api/account-recovery", new { phone = Input(phone), channel = "whatsapp", nonce, previousEmail });
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
    }
    private static async Task AddPhoneAsync(IServiceProvider services, Guid userId, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var number = PhoneNumber.Create(phone).Value;
            await provider.GetRequiredService<IExternalLoginLock>().LockPhoneAsync(number, ct);
            var methods = provider.GetRequiredService<ILoginMethodRepository>();
            await methods.LockUserAsync(userId, ct);
            var method = LoginMethod.CreatePhone(userId, number);
            Assert.True((await provider.GetRequiredService<LoginMethodLinker>().VerifyAsync(method, provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, ct)).IsSuccess);
            methods.Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> app)
    {
        var client = Client(app);
        var account = await AccountJourney.RegisterAsync(app, client, Ct);
        await PlatformOperators.GrantAsync(app.Services, account.UserId, [PlatformPermissions.Recoveries.Manage], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }
    private static async Task<JsonElement> RowAsync(HttpClient client, string phone)
    {
        using var response = await client.GetAsync("/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(phone), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return Assert.Single(json!.RootElement.GetProperty("items").EnumerateArray()).Clone();
    }
    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, JsonElement row, string action) => PostAsync(client,
        $"/api/platform/recoveries/{row.GetProperty("id").GetGuid():D}/{action}", new { version = row.GetProperty("version").GetUInt32(), reason = "Confirmó los datos por soporte." });
    private static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return json!.RootElement.GetProperty("code").GetString();
    }
    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string code, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(code, json!.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }
    private static async Task<Guid[]> OutboxIdsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().Select(value => value.Id).ToArrayAsync(Ct);
    }
    private static async Task<string[]> TemplatesAsync(IServiceProvider services, string phone, Guid[] before)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var protector = provider.GetRequiredService<IPayloadProtector>();
        var payloads = await provider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .Where(value => value.Channel == "whatsapp" && !before.Contains(value.Id) && value.EncryptedPayload.Length > 0)
            .Select(value => value.EncryptedPayload).ToArrayAsync(Ct);
        return payloads.Select(payload => JsonDocument.Parse(protector.Unprotect(payload))).Select(json =>
        {
            using (json) return json.RootElement.GetProperty("To").GetString() == phone ? json.RootElement.GetProperty("TemplateName").GetString() : null;
        }).OfType<string>().ToArray();
    }
}
