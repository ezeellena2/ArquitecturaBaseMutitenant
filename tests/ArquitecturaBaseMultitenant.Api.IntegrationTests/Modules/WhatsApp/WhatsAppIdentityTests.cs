using ArquitecturaBaseMultitenant.Domain.Auditing;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Hybrid;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

public sealed class WhatsAppIdentityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Phone = "+5491134567890";

    [Theory]
    [InlineData("verify")]
    [InlineData("resend")]
    [InlineData("remove")]
    public async Task Waiting_method_change_reads_state_after_identity_and_account_locks(string change)
    {
        var phone = NewPhone();
        var gate = new MethodIdentityGate(phone);
        await using var application = Host().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var descriptor = Assert.Single(services, value => value.ServiceType == typeof(IExternalLoginLock));
            services.Remove(descriptor);
            services.AddScoped<IExternalLoginLock>(provider => new PausedMethodIdentityLock(
                (IExternalLoginLock)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!), gate));
        }));
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(application, client, Ct);
        using var added = await PostAsync(client, "/api/me/login-methods", Contact(phone), Ct);
        using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var methodId = body!.RootElement.GetProperty("methodId").GetGuid();
        var code = await CodeAsync(application.Services, phone);
        gate.Armed = true;
        var first = PostAsync(client, $"/api/me/login-methods/{methodId}/verify", new { code }, Ct);
        await gate.FirstReached.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var second = change == "remove" ? client.DeleteAsync($"/api/me/login-methods/{methodId}", Ct)
            : PostAsync(client, $"/api/me/login-methods/{methodId}/{(change == "verify" ? "verify" : "code")}", new { code }, Ct);
        await gate.SecondReached.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        gate.ReleaseFirst.TrySetResult();
        using var verified = await first;
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        if (change == "remove")
        {
            using var primary = await client.PutAsJsonAsync($"/api/me/login-methods/{methodId}/primary", new { }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, primary.StatusCode);
        }
        gate.ReleaseSecond.TrySetResult();
        using var changed = await second;
        Assert.Equal(change == "remove" ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, changed.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var method = await context.LoginMethods.AsNoTracking().SingleOrDefaultAsync(value => value.Id == methodId, Ct);
        if (change == "remove")
        {
            Assert.Null(method);
            var primary = Assert.Single(await context.LoginMethods.AsNoTracking().Where(value => value.UserId == account.UserId && value.IsPrimary).ToArrayAsync(Ct));
            Assert.Equal(LoginMethodType.Email, primary.Type);
            var user = await context.Users.AsNoTracking().SingleAsync(value => value.Id == account.UserId, Ct);
            Assert.Equal(account.Email.Value, user.Email);
            Assert.Null(user.PhoneNumber);
        }
        else
        {
            Assert.NotNull(method);
            Assert.NotNull(method.VerifiedAtUtc);
            using var problem = await changed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Identity.LoginMethod.AlreadyVerified", problem!.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Login_request_holds_identity_account_and_code_locks_before_admitting_delivery()
    {
        var phone = NewPhone();
        var probe = new LockProbe();
        await using var application = Host().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ILoginCodeAdmission>(provider => new LockProbeAdmission(
                provider.GetRequiredService<ApplicationDbContext>(), provider.GetRequiredService<IUserLookup>(), probe))));
        using var client = application.CreateClient();
        await application.Services.SeedDatabaseAsync(Ct);
        await StagePhoneAsync(application.Services, phone);
        using var requested = await PostAsync(client, "/api/auth/login-code", Contact(phone), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.Equal(new[] { true, true, true }, probe.Held);
    }

    [Fact]
    public async Task Phone_signup_creates_personal_account_without_email_and_login_uses_only_verified_method()
    {
        await using var application = factory.WithWebHostBuilder(builder => builder.UseSetting("WhatsApp:PhoneNumberId", "identity-number")
            .UseSetting("WhatsApp:AccessToken", "fixture-token").UseSetting("WhatsApp:AppSecret", "fixture-secret")
            .UseSetting("WhatsApp:VerifyToken", "fixture-verify"));
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await application.Services.SeedDatabaseAsync(Ct);
        using var requested = await PostAsync(client, "/api/auth/signup", Body(), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await CodeAsync(application.Services, Phone);
        using var withoutConsent = await PostAsync(client, "/api/auth/signup/verify", new { phone = new { country = "AR", number = Phone }, channel = "whatsapp", acceptedTerms = false, code }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutConsent.StatusCode);
        Assert.False(withoutConsent.Headers.Contains("Set-Cookie"));
        using var verified = await PostAsync(client, "/api/auth/signup/verify", Body(code), Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Application", StringComparison.Ordinal));
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUserIdAsync(LoginMethodType.Phone, Phone, Ct);
        Assert.NotNull(id);
        var user = await context.Users.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Null(user.Email);
        Assert.Equal(Phone, user.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.Equal(2, await context.LegalAcceptances.CountAsync(value => value.UserId == id, Ct));
        using var taken = await PostAsync(client, "/api/auth/signup", Body(), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        using var missing = await PostAsync(client, "/api/auth/login-code", new { phone = new { country = "AR", number = "+5491145678901" }, channel = "whatsapp" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Theory]
    [InlineData("consumer", false)]
    [InlineData("business", false)]
    [InlineData("consumer", true)]
    [InlineData("business", true)]
    [InlineData("platform", true)]
    public async Task Verified_phone_survives_country_restriction_and_operator_is_private_until_otp(string access, bool platformOperator)
    {
        var phone = NewPhone();
        await using var application = Host("US");
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await application.Services.SeedDatabaseAsync(Ct);
        var id = await StagePhoneAsync(application.Services, phone, platformOperator);
        using var requested = await PostAsync(client, "/api/auth/login-code", Contact(phone), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.False(requested.Headers.Contains("Set-Cookie"));
        var code = await CodeAsync(application.Services, phone);
        using var verified = await PostAsync(client, "/api/auth/login-code/verify", new { phone = new { country = "US", number = phone }, channel = "whatsapp", code, returnUrl = AuthorizePath(access) }, Ct);
        if (platformOperator && access != "platform")
        {
            Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
            Assert.False(verified.Headers.Contains("Set-Cookie"));
            using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Tenancy.Access.PlatformOnly", problem!.RootElement.GetProperty("code").GetString());
        }
        else if (access == "business")
        {
            // An identity with no organization retains the business NotMember result.
            Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
            Assert.False(verified.Headers.Contains("Set-Cookie"));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            Assert.Contains(verified.Headers.GetValues("Set-Cookie"), value => value.Contains(access == "platform" ? "Identity.Platform=" : "Identity.Application=", StringComparison.Ordinal));
        }
        await using var scope = application.Services.CreateAsyncScope();
        Assert.Equal(id, await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUserIdAsync(LoginMethodType.Phone, phone, Ct));
    }

    [Theory]
    [InlineData("+12025550123", "WhatsApp.Phone.CountryNotAllowed")]
    [InlineData("+442079460018", "Users.Phone.NotMobile")]
    public async Task New_phone_rejects_actual_foreign_country_or_fixed_line_without_code(string phone, string error)
    {
        await using var application = Host();
        using var client = application.CreateClient();
        await application.Services.SeedDatabaseAsync(Ct);
        using var rejected = await PostAsync(client, "/api/auth/signup", new { phone = new { country = "AR", number = phone }, channel = "whatsapp", acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(error, problem!.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("phone", out _));
        await using var scope = application.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AnyAsync(value => value.Destination == phone, Ct));
    }

    [Theory]
    [InlineData(false, BusinessSignupMode.Open)]
    [InlineData(true, BusinessSignupMode.Open)]
    [InlineData(false, BusinessSignupMode.RequiresApproval)]
    [InlineData(true, BusinessSignupMode.RequiresApproval)]
    public async Task Business_phone_signup_is_independent_and_otp_cannot_register_consumer(bool existing, BusinessSignupMode mode)
    {
        var phone = NewPhone();
        await using var application = Host();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await application.Services.SeedDatabaseAsync(Ct);
        Guid? previous = existing ? await StagePhoneAsync(application.Services, phone) : null;
        await ConfigureSignupAsync(application.Services, ConsumerSignupMode.Closed, mode);
        try
        {
            using var requested = await PostAsync(client, "/api/auth/business-signup/code", SignupBody(phone), Ct);
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var code = await CodeAsync(application.Services, phone);
            using var crossed = await PostAsync(client, "/api/auth/signup/verify", SignupBody(phone, code), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, crossed.StatusCode);
            using var verified = await PostAsync(client, "/api/auth/business-signup/verify", SignupBody(phone, code), Ct);
            Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
            await using var scope = application.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var id = await scope.ServiceProvider.GetRequiredService<IUserLookup>().FindVerifiedUserIdAsync(LoginMethodType.Phone, phone, Ct);
            Assert.NotNull(id);
            if (existing) Assert.Equal(previous, id);
            Assert.Empty(await context.UserTenantAccesses.Where(value => value.UserId == id).ToArrayAsync(Ct));
            Assert.Equal(2, await context.LegalAcceptances.CountAsync(value => value.UserId == id, Ct));
            using var created = await CreateBusinessAsync(client);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            using var result = await created.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(mode == BusinessSignupMode.RequiresApproval ? "PendingApproval" : "Active", result!.RootElement.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("notificationEmail").ValueKind);
            using var limited = await CreateBusinessAsync(client);
            Assert.Equal(HttpStatusCode.Conflict, limited.StatusCode);
        }
        finally { await ConfigureSignupAsync(application.Services, ConsumerSignupMode.Open, BusinessSignupMode.Open); }
    }

    [Fact]
    public async Task Closed_business_signup_rejects_phone_without_code_and_operator_requires_proof()
    {
        var phone = NewPhone();
        await using var application = Host();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await application.Services.SeedDatabaseAsync(Ct);
        var id = await StagePhoneAsync(application.Services, phone, platformOperator: true);
        await ConfigureSignupAsync(application.Services, ConsumerSignupMode.Open, BusinessSignupMode.Closed);
        try
        {
            using var closed = await PostAsync(client, "/api/auth/business-signup/code", SignupBody(phone), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);
            await using var scope = application.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await context.LoginCodes.AnyAsync(value => value.Destination == phone, Ct));
            await ConfigureSignupAsync(application.Services, ConsumerSignupMode.Open, BusinessSignupMode.Open);
            using var requested = await PostAsync(client, "/api/auth/business-signup/code", SignupBody(phone), Ct);
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            Assert.False(requested.Headers.Contains("Set-Cookie"));
            var code = await CodeAsync(application.Services, phone);
            using var verified = await PostAsync(client, "/api/auth/business-signup/verify", SignupBody(phone, code), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
            Assert.False(verified.Headers.Contains("Set-Cookie"));
            using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Tenancy.Access.PlatformOnly", problem!.RootElement.GetProperty("code").GetString());
            Assert.False(await context.UserTenantAccesses.AnyAsync(value => value.UserId == id, Ct));
        }
        finally { await ConfigureSignupAsync(application.Services, ConsumerSignupMode.Open, BusinessSignupMode.Open); }
    }

    [Fact]
    public async Task Account_add_phone_proves_only_new_destination_notifies_others_and_keeps_last_method()
    {
        var phone = NewPhone();
        await using var application = Host();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(application, client, Ct);
        using var added = await PostAsync(client, "/api/me/login-methods", Contact(phone), Ct);
        Assert.Equal(HttpStatusCode.Accepted, added.StatusCode);
        using var addedBody = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var methodId = addedBody!.RootElement.GetProperty("methodId").GetGuid();
        var code = await CodeAsync(application.Services, phone);
        using var crossed = await PostAsync(client, "/api/auth/login-code/verify", new { phone = new { country = "AR", number = phone }, channel = "whatsapp", code, returnUrl = AuthorizePath("consumer") }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, crossed.StatusCode);
        using var verified = await PostAsync(client, $"/api/me/login-methods/{methodId}/verify", new { code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var primary = await client.PutAsJsonAsync($"/api/me/login-methods/{methodId}/primary", new { }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, primary.StatusCode);
        await using var scope = application.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailMethod = await context.LoginMethods.AsNoTracking().SingleAsync(value => value.UserId == account.UserId && value.Type == LoginMethodType.Email, Ct);
        var user = await context.Users.AsNoTracking().SingleAsync(value => value.Id == account.UserId, Ct);
        Assert.Null(user.Email);
        Assert.Equal(phone, user.PhoneNumber);
        var payloads = await context.OutboxMessages.AsNoTracking().Where(value => value.Channel == "whatsapp" && value.UserId == account.UserId && value.EncryptedPayload.Length > 0)
            .Select(value => value.EncryptedPayload).ToArrayAsync(Ct);
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        Assert.All(payloads, payload => Assert.Contains("codigo_ingreso", protector.Unprotect(payload)));
        using var removed = await client.DeleteAsync($"/api/me/login-methods/{emailMethod.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var last = await client.DeleteAsync($"/api/me/login-methods/{methodId}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
    }

    [Fact]
    public async Task Pending_phone_revalidates_ownership_on_resend_and_verify_without_notifying_current_owner()
    {
        var phone = NewPhone();
        await using var application = Host();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(application, client, Ct);
        using var added = await PostAsync(client, "/api/me/login-methods", Contact(phone), Ct);
        using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var methodId = body!.RootElement.GetProperty("methodId").GetGuid();
        var code = await CodeAsync(application.Services, phone);
        var owner = await StagePhoneAsync(application.Services, phone);
        using var resent = await PostAsync(client, $"/api/me/login-methods/{methodId}/code", new { }, Ct);
        using var verified = await PostAsync(client, $"/api/me/login-methods/{methodId}/verify", new { code }, Ct);
        foreach (var rejected in new[] { resent, verified })
        {
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Identity.LoginMethod.PhoneAlreadyUsed", problem!.RootElement.GetProperty("code").GetString());
        }
        await using var scope = application.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AnyAsync(value => value.UserId == owner, Ct));
    }

    [Fact]
    public async Task Concurrent_signup_verifications_consume_once_and_create_only_one_owner()
    {
        var phone = NewPhone();
        await using var application = Host();
        using var client = application.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await application.Services.SeedDatabaseAsync(Ct);
        using var requested = await PostAsync(client, "/api/auth/signup", SignupBody(phone), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await CodeAsync(application.Services, phone);
        var responses = await Task.WhenAll(PostAsync(client, "/api/auth/signup/verify", SignupBody(phone, code), Ct), PostAsync(client, "/api/auth/signup/verify", SignupBody(phone, code), Ct));
        try
        {
            Assert.Single(responses, value => value.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, value => value.StatusCode == HttpStatusCode.BadRequest);
            await using var scope = application.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.CountAsync(value => value.Type == LoginMethodType.Phone && value.Value == phone && value.VerifiedAtUtc != null, Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Pending_phone_is_a_new_destination_when_allowed_countries_change()
    {
        var phone = NewPhone();
        await using var original = Host();
        using var client = original.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(original, client, Ct);
        using var added = await PostAsync(client, "/api/me/login-methods", Contact(phone), Ct);
        using var body = await added.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var methodId = body!.RootElement.GetProperty("methodId").GetGuid();
        var code = await CodeAsync(original.Services, phone);
        await using var restricted = Host("US");
        using var initialize = restricted.CreateClient();
        await using var scope = restricted.Services.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Services.Identity.LoginMethodIssuer>();
        var verification = scope.ServiceProvider.GetRequiredService<ArquitecturaBaseMultitenant.Application.Services.Identity.LoginMethodVerifier>();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var resend = await unit.ExecuteInTransactionAsync(ct => current.SendAsync(account.UserId, methodId, ct), CommitPolicy.OnAnyResult, Ct);
        Assert.Equal("WhatsApp.Phone.CountryNotAllowed", resend.Error.Code);
        var verify = await unit.ExecuteInTransactionAsync(ct => verification.VerifyAsync(account.UserId, new ArquitecturaBaseMultitenant.Application.Models.Identity.VerifyLoginMethodRequest(methodId, code), ct), CommitPolicy.OnAnyResult, Ct);
        Assert.Equal("WhatsApp.Phone.CountryNotAllowed", verify.Error.Code);
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null((await context.LoginMethods.SingleAsync(value => value.Id == methodId, Ct)).VerifiedAtUtc);
    }

    private WebApplicationFactory<Program> Host(string country = "AR") => factory.WithWebHostBuilder(builder => builder
        .UseSetting("WhatsApp:PhoneNumberId", "identity-number").UseSetting("WhatsApp:AccessToken", "fixture-token")
        .UseSetting("WhatsApp:AppSecret", "fixture-secret").UseSetting("WhatsApp:VerifyToken", "fixture-verify")
        .UseSetting("WhatsApp:AllowedCountries:0", country).UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "0")
        .UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "100"));
    private static string NewPhone() => "+54911" + RandomNumberGenerator.GetInt32(30000000, 59999999).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static object Contact(string phone) => new { phone = new { country = "AR", number = phone }, channel = "whatsapp" };
    private static object SignupBody(string phone, string? code = null) => new { phone = new { country = "US", number = phone }, channel = "whatsapp", acceptedTerms = true, code };
    private static string AuthorizePath(string access) => "/connect/authorize?client_id=web&response_type=code&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback&scope=openid%20profile%20email%20api&code_challenge="
        + WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~"))) + "&code_challenge_method=S256&access=" + access;
    private static async Task<Guid> StagePhoneAsync(IServiceProvider services, string phone, bool platformOperator = false)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        Guid id = default;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var linker = provider.GetRequiredService<LoginMethodLinker>();
            var value = PhoneNumber.Create(phone).Value;
            await provider.GetRequiredService<IExternalLoginLock>().LockPhoneAsync(value, ct);
            var user = await provider.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
            id = user.Id;
            if (platformOperator) await provider.GetRequiredService<IUserRepository>().SetPlatformOperatorAsync(id, true, ct);
            var method = LoginMethod.CreatePhone(id, value);
            Assert.True((await linker.VerifyAsync(method, provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, ct)).IsSuccess);
            provider.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return id;
    }
    private static async Task ConfigureSignupAsync(IServiceProvider services, ConsumerSignupMode consumer, BusinessSignupMode business)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var settings = await scope.ServiceProvider.GetRequiredService<IPlatformSettingsRepository>().GetAsync(ct);
            scope.ServiceProvider.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty, "T04 signup fixture", scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return settings!.Update(consumer, business, 1, Email.Create("sales@example.test").Value, Email.Create("support@example.test").Value);
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<HybridCache>().RemoveAsync("p:settings", Ct);
    }

    private static object Body(string? code = null) => new { phone = new { country = "AR", number = "011 15 3456 7890" }, channel = "whatsapp", acceptedTerms = true, code };

    private static async Task<HttpResponseMessage> CreateBusinessAsync(HttpClient client)
    {
        using var antiforgery = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
        using var token = await antiforgery.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/business-signup")
        {
            Content = JsonContent.Create(new { organizationName = "Empresa Phone", slug = "phone-" + Guid.NewGuid().ToString("N")[..12], firstCompanyName = "Primera empresa", acceptedTerms = true }),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        request.Headers.Add("RequestVerificationToken", token!.RootElement.GetProperty("requestToken").GetString());
        return await client.SendAsync(request, Ct);
    }

    internal static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        return await client.SendAsync(request, ct);
    }

    internal static async Task<string> CodeAsync(IServiceProvider services, string phone)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        foreach (var row in await context.OutboxMessages.AsNoTracking().Where(value => value.Channel == "whatsapp").OrderByDescending(value => value.CreatedAtUtc).ToArrayAsync(Ct))
        {
            if (string.IsNullOrEmpty(row.EncryptedPayload)) continue;
            using var payload = JsonDocument.Parse(protector.Unprotect(row.EncryptedPayload));
            var root = payload.RootElement;
            if (root.GetProperty("To").GetString() == phone && root.GetProperty("TemplateName").GetString() == "codigo_ingreso")
                return root.GetProperty("BodyParameters")[0].GetString()!;
        }
        throw new Xunit.Sdk.XunitException("The authentication outbox payload was not found.");
    }

    private sealed class LockProbe
    {
        internal List<bool> Held { get; } = [];
    }

    private sealed class MethodIdentityGate(string phone)
    {
        private int requests;
        internal bool Armed { get; set; }
        internal TaskCompletionSource FirstReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseSecond { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal async Task BeforeAsync(PhoneNumber value, CancellationToken ct)
        {
            if (!Armed || value.Value != phone) return;
            var order = Interlocked.Increment(ref requests);
            (order == 1 ? FirstReached : SecondReached).TrySetResult();
            await (order == 1 ? ReleaseFirst : ReleaseSecond).Task.WaitAsync(ct);
        }
    }

    private sealed class PausedMethodIdentityLock(IExternalLoginLock inner, MethodIdentityGate gate) : IExternalLoginLock
    {
        public async Task LockPhoneAsync(PhoneNumber phone, CancellationToken ct)
        {
            await gate.BeforeAsync(phone, ct);
            await inner.LockPhoneAsync(phone, ct);
        }
        public Task LockEmailAsync(Email email, CancellationToken ct) => inner.LockEmailAsync(email, ct);
        public Task AcquireAsync(string subject, Email? email, CancellationToken ct) => inner.AcquireAsync(subject, email, ct);
    }

    private sealed class LockProbeAdmission(ApplicationDbContext context, IUserLookup users, LockProbe probe) : ILoginCodeAdmission
    {
        public string Channel => "whatsapp";
        public async Task<Result> ReserveAsync(LoginCodeDestination destination, CancellationToken ct)
        {
            var userId = await users.FindVerifiedUserIdAsync(LoginMethodType.Phone, destination.Value, ct);
            Assert.NotNull(userId);
            await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
            await connection.OpenAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var key in new[] { AdvisoryLockKeys.ExternalPhone(destination.Value), AdvisoryLockKeys.Account(userId.Value), AdvisoryLockKeys.LoginCode(destination) })
            {
                await using var command = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction);
                command.Parameters.AddWithValue("key", key);
                probe.Held.Add(!(bool)(await command.ExecuteScalarAsync(ct))!);
            }
            return Result.Success();
        }
    }
}
