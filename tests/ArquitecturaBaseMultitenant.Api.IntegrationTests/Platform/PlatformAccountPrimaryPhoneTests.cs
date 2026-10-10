using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Identidad principal global en Cuentas, por HTTP y PostgreSQL real; sin depender de un módulo Phone.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformAccountPrimaryPhoneTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("PhoneOnly", true)]
    [InlineData("PhoneEmail", true)]
    [InlineData("EmailPhone", false)]
    [InlineData("GooglePhone", false)]
    [InlineData("Pending", false)]
    [InlineData("Deleted", false)]
    [InlineData("LegacyCopy", false)]
    public async Task List_and_detail_mask_only_the_verified_primary_phone(string identity, bool showsPhone)
    {
        using var platform = await OperatorAsync();
        var account = await SeedAsync(factory.Services, identity);
        var expected = showsPhone ? PersonalDataMask.Phone(account.Phone) : null;
        await AssertIdentityAsync(platform, account, expected);
    }

    [Fact]
    public async Task A_country_closed_to_new_signups_keeps_the_verified_primary_identity()
    {
        var channel = new ExistingPhoneChannel();
        await using var enabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoginCodeChannel>(channel)));
        using var platform = enabled.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthorizeOperatorAsync(platform);
        var account = await SeedAsync(enabled.Services, "PhoneOnly");
        Assert.True(channel.IsAvailable);
        Assert.Empty(channel.Countries);
        Assert.True(channel.ValidateNewDestination(LoginCodeDestination.ForPhone(PhoneNumber.Create(account.Phone).Value, channel.Key), "AR").IsFailure);
        await AssertIdentityAsync(platform, account, PersonalDataMask.Phone(account.Phone));
        var detail = await GetAsync(platform, account.Id.ToString("D"));
        Assert.True(Assert.Single(detail.GetProperty("loginMethods").EnumerateArray()).GetProperty("canSignIn").GetBoolean());
    }

    [Fact]
    public async Task Reopening_list_and_detail_follows_primary_changes_and_removal()
    {
        await using var enabled = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ILoginCodeChannel>(new ExistingPhoneChannel())));
        using var platform = enabled.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthorizeOperatorAsync(platform);
        var account = await SeedAsync(enabled.Services, "PhoneEmail");
        await AssertIdentityAsync(platform, account, PersonalDataMask.Phone(account.Phone));

        await using var scope = enabled.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var sessionUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var management = ActivatorUtilities.CreateInstance<LoginMethodManagementService>(services,
            new AccountActor(account.Id, sessionUtc));
        Assert.True((await management.MakePrimaryAsync(new ChangeLoginMethodRequest(account.EmailMethodId!.Value), Ct)).IsSuccess);
        await AssertIdentityAsync(platform, account, null);
        Assert.True((await management.MakePrimaryAsync(new ChangeLoginMethodRequest(account.PhoneMethodId!.Value), Ct)).IsSuccess);
        await AssertIdentityAsync(platform, account, PersonalDataMask.Phone(account.Phone));
        Assert.True((await management.RemoveAsync(new ChangeLoginMethodRequest(account.PhoneMethodId.Value), Ct)).IsSuccess);
        await AssertIdentityAsync(platform, account, null, search: account.Email);
    }

    [Fact]
    public async Task Approving_recovery_replaces_the_primary_phone_identity_on_the_next_read()
    {
        using var platform = await OperatorAsync();
        var account = await SeedAsync(factory.Services, "PhoneOnly");
        await AssertIdentityAsync(platform, account, PersonalDataMask.Phone(account.Phone));
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var newEmail = "recovered-" + Guid.NewGuid().ToString("N") + "@example.test";
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var requested = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/code", new { email = newEmail, nonce }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, Ct);
        using var verified = await AccountJourney.PostAsync(anonymous, "/api/account-recovery/verify", new { email = newEmail, nonce, code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(anonymous, "/api/account-recovery",
            new { email = newEmail, nonce, previousPhone = new { country = "AR", number = account.Phone } }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var page = await GetPathAsync(platform, "/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(newEmail));
        var request = Assert.Single(page.GetProperty("items").EnumerateArray());
        using var approved = await PlatformJourney.PostAsync(platform,
            $"/api/platform/recoveries/{request.GetProperty("id").GetGuid():D}/approve",
            new { version = request.GetProperty("version").GetUInt32(), reason = "Recuperación de identidad principal." }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await AssertIdentityAsync(platform, account, null);
        var detail = await GetAsync(platform, account.Id.ToString("D"));
        Assert.Equal(newEmail, detail.GetProperty("primaryEmail").GetString());
        Assert.Contains(detail.GetProperty("loginMethods").EnumerateArray(), method =>
            method.GetProperty("type").GetString() == "Phone" && !method.GetProperty("isPrimary").GetBoolean());
    }

    private async Task<HttpClient> OperatorAsync()
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthorizeOperatorAsync(client);
        return client;
    }

    private async Task AuthorizeOperatorAsync(HttpClient client)
    {
        var viewer = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, viewer.UserId,
            [PlatformPermissions.Accounts.Read, PlatformPermissions.Recoveries.Manage], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
    }

    private static async Task AssertIdentityAsync(HttpClient client, Seeded account, string? expected, string? search = null)
    {
        var page = await GetAsync(client, "paged?search=" + Uri.EscapeDataString(search ?? account.Search));
        var row = Assert.Single(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == account.Id);
        var detail = await GetAsync(client, account.Id.ToString("D"));
        Assert.Equal(expected, row.GetProperty("maskedPrimaryPhone").GetString());
        Assert.Equal(expected, detail.GetProperty("maskedPrimaryPhone").GetString());
        Assert.DoesNotContain(account.Phone, page.GetRawText());
        Assert.DoesNotContain(account.Phone, detail.GetRawText());
        Assert.False(row.TryGetProperty("phoneNumber", out _));
        Assert.False(detail.TryGetProperty("phoneNumber", out _));
        if (account.NoNameOrEmail && expected is not null)
        {
            Assert.Equal(JsonValueKind.Null, row.GetProperty("displayName").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("primaryEmail").ValueKind);
        }
    }

    private static Task<JsonElement> GetAsync(HttpClient client, string path) => GetPathAsync(client, "/api/platform/accounts/" + path);

    private static async Task<JsonElement> GetPathAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private sealed record Seeded(Guid Id, string Phone, string Email, string Search, Guid? PhoneMethodId, Guid? EmailMethodId, bool NoNameOrEmail);

    private static async Task<Seeded> SeedAsync(IServiceProvider provider, string identity)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
        var noNameOrEmail = identity is "PhoneOnly" or "PhoneEmail";
        var user = ApplicationUser.Create(noNameOrEmail ? null : "Persona " + Guid.NewGuid().ToString("N"),
            "es-AR", "America/Argentina/Buenos_Aires").Value;
        var phone = "+54911" + RandomNumberGenerator.GetInt32(20000000, 99999999);
        var email = "backup-" + user.Id.ToString("N") + "@example.test";
        var phoneMethod = LoginMethod.CreatePhone(user.Id, PhoneNumber.Create(phone).Value);
        var emailMethod = identity is "PhoneEmail" or "EmailPhone"
            ? LoginMethod.CreateEmail(user.Id, Email.Create(email).Value) : null;
        var google = identity == "GooglePhone" ? LoginMethod.CreateGoogle(user.Id, "subject-" + user.Id.ToString("N"), Email.Create(email).Value) : null;
        if (identity != "Pending") Assert.True(phoneMethod.Verify(nowUtc).IsSuccess);
        if (identity is "PhoneOnly" or "PhoneEmail" or "Deleted") Assert.True(phoneMethod.MakePrimary().IsSuccess);
        if (emailMethod is not null)
        {
            Assert.True(emailMethod.Verify(nowUtc).IsSuccess);
            if (identity == "EmailPhone") Assert.True(emailMethod.MakePrimary().IsSuccess);
        }
        if (google is not null)
        {
            Assert.True(google.Verify(nowUtc).IsSuccess);
            Assert.True(google.MakePrimary().IsSuccess);
        }
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async _ =>
        {
            Assert.True((await services.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            if (identity != "LegacyCopy") db.LoginMethods.Add(phoneMethod);
            if (emailMethod is not null) db.LoginMethods.Add(emailMethod);
            if (google is not null) db.LoginMethods.Add(google);
            // Datos históricos: ni una copia de Identity ni un principal sin verificar o eliminado son evidencia.
            db.Entry(user).Property(item => item.PhoneNumber).CurrentValue = phone;
            if (identity == "Pending") db.Entry(phoneMethod).Property(item => item.IsPrimary).CurrentValue = true;
            if (identity == "Deleted") db.Entry(user).Property(item => item.Status).CurrentValue = UserStatus.Deleted;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
        return new Seeded(user.Id, phone, email, identity == "LegacyCopy" ? user.DisplayName! : phone,
            identity == "LegacyCopy" ? null : phoneMethod.Id, emailMethod?.Id, noNameOrEmail);
    }

    private sealed record AccountActor(Guid? UserId, DateTime? SessionStartedAtUtc) : ICurrentUser
    {
        public Access? Access => ArquitecturaBaseMultitenant.Domain.Users.Access.Consumer;
    }

    // Capacidad neutral para métodos existentes. No habilita altas ni simula entrega de mensajes de un proveedor.
    private sealed class ExistingPhoneChannel : ILoginCodeChannel
    {
        public string Key => "existing-phone";
        public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable => true;
        public IReadOnlyList<string> Countries => [];
        public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => PhoneErrors.Invalid;
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw new NotSupportedException();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw new NotSupportedException();
    }
}
