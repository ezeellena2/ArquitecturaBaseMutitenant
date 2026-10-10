using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Identidad principal Phone de Mi cuenta por HTTP y PostgreSQL, con canales neutrales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class MePrimaryPhoneTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("PhoneOnly", true)]
    [InlineData("PhoneEmail", true)]
    [InlineData("EmailPhone", false)]
    [InlineData("GooglePhone", false)]
    [InlineData("Pending", false)]
    public async Task Me_masks_only_the_verified_global_primary_phone(string identity, bool showsPhone)
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var seeded = await SeedAsync(factory.Services, account, identity);

        var profile = await AssertMaskAsync(client, seeded.Phone, showsPhone);

        Assert.Equal("consumer", profile.GetProperty("access").GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("displayName").ValueKind);
        if (identity is "PhoneOnly" or "PhoneEmail")
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("email").ValueKind);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("business")]
    [InlineData("platform")]
    public async Task Each_access_keeps_its_scope_email_permissions_and_profile_version(string access)
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        Guid? tenantId = null;
        if (access == "business")
            tenantId = await OrganizationJourney.StageAsync(factory, account, "Identidad de Mi cuenta", Ct);
        if (access == "platform")
            await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Accounts.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, access, Ct, tenantId);
        var before = await ReadAsync(client);
        var seeded = await SeedAsync(factory.Services, account, "PhoneEmail", tenantId);

        var after = await AssertMaskAsync(client, seeded.Phone, true);

        foreach (var field in new[] { "access", "activeTenantId", "hasPersonalSpace", "organizations", "effectivePermissions", "permissions", "version", "culture", "timeZoneId", "features", "moduleCapabilities", "pendingLegalDocuments", "firstName", "lastName" })
            Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        Assert.Equal(tenantId is null ? null : seeded.ManagedEmail, after.GetProperty("email").GetString());
    }

    [Fact]
    public async Task A_country_closed_to_new_signups_and_a_disabled_channel_keep_the_primary_identity()
    {
        var channel = new ExistingPhoneChannel();
        await using var enabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoginCodeChannel>(channel)));
        using var client = enabled.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(enabled, client, Ct);
        var seeded = await SeedAsync(enabled.Services, account, "PhoneOnly");
        Assert.Empty(channel.Countries);
        Assert.True(channel.ValidateNewDestination(LoginCodeDestination.ForPhone(PhoneNumber.Create(seeded.Phone).Value, channel.Key), "AR").IsFailure);
        var available = await AssertMaskAsync(client, seeded.Phone, true);
        Assert.False(available.GetProperty("needsPersonalLoginMethod").GetBoolean());

        channel.IsAvailable = false;
        var disabled = await AssertMaskAsync(client, seeded.Phone, true);
        Assert.True(disabled.GetProperty("needsPersonalLoginMethod").GetBoolean());
    }

    [Fact]
    public async Task Reopening_me_follows_primary_changes_and_removal()
    {
        await using var enabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoginCodeChannel>(new ExistingPhoneChannel())));
        using var client = enabled.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(enabled, client, Ct);
        var seeded = await SeedAsync(enabled.Services, account, "PhoneEmail");
        await AssertMaskAsync(client, seeded.Phone, true);
        await using var scope = enabled.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var management = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services,
            new AccountActor(account.UserId, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));

        Assert.True((await management.MakePrimaryAsync(new ChangeLoginMethodRequest(seeded.EmailId), Ct)).IsSuccess);
        var emailPrimary = await AssertMaskAsync(client, seeded.Phone, false);
        Assert.Equal(account.Email.Value, emailPrimary.GetProperty("email").GetString());
        Assert.True((await management.MakePrimaryAsync(new ChangeLoginMethodRequest(seeded.PhoneId), Ct)).IsSuccess);
        await AssertMaskAsync(client, seeded.Phone, true);
        Assert.True((await management.RemoveAsync(new ChangeLoginMethodRequest(seeded.PhoneId), Ct)).IsSuccess);
        await AssertMaskAsync(client, seeded.Phone, false);
    }

    [Fact]
    public async Task Approved_recovery_changes_the_identity_on_the_next_authenticated_read()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var seeded = await SeedAsync(factory.Services, account, "PhoneOnly");
        await AssertMaskAsync(client, seeded.Phone, true);
        using var platform = Client();
        var viewer = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId, [PlatformPermissions.Recoveries.Manage], Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var recovered = Client();
        var email = "recovered-me-" + Guid.NewGuid().ToString("N") + "@example.test";
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var requested = await AccountJourney.PostAsync(recovered, "/api/account-recovery/code", new { email, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(recovered, "/api/account-recovery/verify", new { email, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(recovered, "/api/account-recovery",
            new { email, nonce, previousPhone = new { country = "AR", number = seeded.Phone } }, Ct);
        Assert.True(submitted.StatusCode == HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync(Ct));
        using var read = await platform.GetAsync("/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(email), Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var page = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        var request = Assert.Single(page.RootElement.GetProperty("items").EnumerateArray());
        using var approved = await PlatformJourney.PostAsync(platform,
            $"/api/platform/recoveries/{request.GetProperty("id").GetGuid():D}/approve",
            new { version = request.GetProperty("version").GetUInt32(), reason = "Identidad recuperada de Mi cuenta." }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        // El código de recuperación pertenece a la preparación; el ingreso real conserva su cooldown global.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var codes = await db.LoginCodes.Where(item => item.Destination == email).ToListAsync(ct);
                var setupAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
                foreach (var item in codes) db.Entry(item).Property(row => row.CreatedAtUtc).CurrentValue = setupAtUtc;
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var loginRequested = await AccountJourney.PostAsync(recovered, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, loginRequested.StatusCode);
        var loginCode = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var loginVerified = await AccountJourney.PostAsync(recovered, "/api/auth/login-code/verify",
            new { email, code = loginCode, returnUrl = "/connect/authorize?access=consumer" }, Ct);
        Assert.Equal(HttpStatusCode.OK, loginVerified.StatusCode);
        await AccountJourney.AuthorizeAsync(recovered, "consumer", Ct);
        var profile = await AssertMaskAsync(recovered, seeded.Phone, false);
        Assert.Equal(account.UserId, profile.GetProperty("id").GetGuid());
        Assert.Equal(email, profile.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Deleted_identity_cannot_read_me_or_expose_a_phone()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var seeded = await SeedAsync(factory.Services, account, "PhoneOnly");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FindAsync([account.UserId], ct);
            Assert.NotNull(user);
            db.Entry(user).Property(item => item.Status).CurrentValue = UserStatus.Deleted;
            await services.GetRequiredService<ISignInService>().RevokeSessionsAsync(account.UserId, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<IAccessStatusCache>().InvalidateUserAsync(account.UserId, Ct);

        using var response = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(seeded.Phone, body);
        Assert.DoesNotContain(PersonalDataMask.Phone(seeded.Phone), body);
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<JsonElement> AssertMaskAsync(HttpClient client, string phone, bool shown)
    {
        var profile = await ReadAsync(client);
        Assert.Equal(shown ? PersonalDataMask.Phone(phone) : null, profile.GetProperty("maskedPrimaryPhone").GetString());
        Assert.DoesNotContain(phone, profile.GetRawText());
        Assert.False(profile.TryGetProperty("phoneNumber", out _));
        return profile;
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.Clone();
    }

    private sealed record Seeded(string Phone, Guid PhoneId, Guid EmailId, string? ManagedEmail);

    private static async Task<Seeded> SeedAsync(IServiceProvider provider, AccountJourney account, string identity, Guid? tenantId = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var phone = "+549115555" + RandomNumberGenerator.GetInt32(1000, 9999);
        var phoneMethod = LoginMethod.CreatePhone(account.UserId, PhoneNumber.Create(phone).Value);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        if (identity != "Pending") Assert.True(phoneMethod.Verify(nowUtc).IsSuccess);
        if (identity is "PhoneOnly" or "PhoneEmail") Assert.True(phoneMethod.MakePrimary().IsSuccess);
        var managedEmail = tenantId is null ? null : "managed-" + account.UserId.ToString("N") + "@example.test";
        Guid emailId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = services.GetRequiredService<ILoginMethodRepository>();
            var methods = await repository.ListByUserIdAsync(account.UserId, ct);
            var email = Assert.Single(methods);
            emailId = email.Id;
            if (identity is "PhoneOnly" or "PhoneEmail" or "GooglePhone" or "Pending")
                await repository.ClearPrimaryAsync(account.UserId, ct);
            if (identity is "PhoneOnly" or "GooglePhone") repository.Remove(email);
            repository.Add(phoneMethod);
            if (identity == "Pending") db.Entry(phoneMethod).Property(item => item.IsPrimary).CurrentValue = true;
            if (identity == "GooglePhone")
            {
                var google = LoginMethod.CreateGoogle(account.UserId, "subject-" + account.UserId.ToString("N"), account.Email);
                Assert.True(google.Verify(nowUtc).IsSuccess);
                Assert.True(google.MakePrimary().IsSuccess);
                repository.Add(google);
            }
            if (managedEmail is not null)
            {
                var managed = LoginMethod.CreateEmail(account.UserId, Email.Create(managedEmail).Value);
                Assert.True(managed.Verify(nowUtc).IsSuccess);
                managed.AssignManagedByOrganization(tenantId!.Value);
                repository.Add(managed);
            }
            await services.GetRequiredService<IUserRepository>().SetPrimaryContactAsync(account.UserId,
                identity is "PhoneOnly" or "PhoneEmail" or "Pending" ? null : account.Email,
                identity is "PhoneOnly" or "PhoneEmail" ? PhoneNumber.Create(phone).Value : null, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return new Seeded(phone, phoneMethod.Id, emailId, managedEmail);
    }

    private sealed record AccountActor(Guid? UserId, DateTime? SessionStartedAtUtc) : ICurrentUser
    {
        public Access? Access => ArquitecturaBaseMultitenant.Domain.Users.Access.Consumer;
    }

    private sealed class ExistingPhoneChannel : ILoginCodeChannel
    {
        public string Key => "existing-phone";
        public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable { get; set; } = true;
        public IReadOnlyList<string> Countries => [];
        public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => PhoneErrors.Invalid;
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw new NotSupportedException();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
    }
}
