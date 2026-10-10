using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

public sealed partial class PlatformRecoveryDestinationConflictTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(LoginMethodType.Email, true)]
    [InlineData(LoginMethodType.Email, false)]
    [InlineData(LoginMethodType.Google, true)]
    [InlineData(LoginMethodType.Google, false)]
    public Task Approval_rejects_an_acquired_destination_without_changing_identity_or_the_pending_request(
        LoginMethodType occupiedType, bool own) => AssertConflictAsync(occupiedType, own, CoreHost, SubmitEmailAsync);

    private static async Task AssertConflictAsync(LoginMethodType occupiedType, bool own,
        Func<ApiFactory, WebApplicationFactory<Program>> createHost,
        Func<WebApplicationFactory<Program>, HttpClient, string, string, Task> submit)
    {
        // Cada caso tiene su propia base: ningún outbox Phone pendiente interfiere con el pickup de otro caso.
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        await using var app = createHost(factory);
        using var person = Client(app);
        var account = await AccountJourney.RegisterAsync(app, person, Ct);
        using var other = Client(app);
        var otherAccount = await AccountJourney.RegisterAsync(app, other, Ct);
        using var platform = Client(app);
        var reviewer = await AccountJourney.RegisterAsync(app, platform, Ct);
        await PlatformOperators.GrantAsync(app.Services, reviewer.UserId, [PlatformPermissions.Recoveries.Manage], Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        using var anonymous = Client(app);
        var destination = occupiedType == LoginMethodType.Phone
            ? "+54911" + RandomNumberGenerator.GetInt32(30000000, 59999999).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"recovery-conflict-{Guid.NewGuid():N}@example.test";
        await submit(app, anonymous, destination, account.Email.Value);
        var original = await RowAsync(platform, destination);
        Assert.Equal("Pending", original.GetProperty("status").GetString());
        var id = original.GetProperty("id").GetGuid();
        var version = original.GetProperty("version").GetUInt32();
        var ownerId = own ? account.UserId : otherAccount.UserId;
        // Transacción propia y prueba legítima del método; no se altera el pedido ni su xmin.
        await OccupyAsync(app.Services, ownerId, occupiedType, destination);
        var before = await SnapshotAsync(app.Services, id, account.UserId, otherAccount.UserId);
        Assert.Equal(version, (await RowAsync(platform, destination)).GetProperty("version").GetUInt32());

        foreach (var language in new[] { "es-AR", "en-US" })
        {
            platform.DefaultRequestHeaders.AcceptLanguage.Clear();
            platform.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
            using var response = await PlatformJourney.PostAsync(platform, $"/api/platform/recoveries/{id:D}/approve",
                new { version, reason = "Confirmó los datos de recuperación." }, Ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Equal(language, Assert.Single(response.Content.Headers.ContentLanguage));
            using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            var body = problem!.RootElement;
            Assert.Equal(own ? AccountRecoveryErrors.MethodAlreadyOnAccountCode : AccountRecoveryErrors.MethodInUseCode,
                body.GetProperty("code").GetString());
            var detail = language == "es-AR"
                ? own ? "Ese método de ingreso ya pertenece a esa cuenta." : "Ese método de ingreso ya es de otra cuenta."
                : own ? "That sign-in method already belongs to that account." : "That sign-in method already belongs to another account.";
            Assert.Equal(detail, body.GetProperty("detail").GetString());
            Assert.False(body.TryGetProperty("errors", out _));
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
            Assert.DoesNotContain(destination, body.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain(ownerId.ToString("D"), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, await SnapshotAsync(app.Services, id, account.UserId, otherAccount.UserId));
            Assert.Equal(original.GetRawText(), (await RowAsync(platform, destination)).GetRawText());
        }
    }

    private static WebApplicationFactory<Program> CoreHost(ApiFactory factory) => factory.WithWebHostBuilder(builder => builder
        .UseEnvironment("Testing").ConfigureTestServices(services => services.RemoveAll<IHostedService>()));

    private static async Task SubmitEmailAsync(WebApplicationFactory<Program> app, HttpClient client, string destination,
        string previousEmail)
    {
        var nonce = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        object Body(string? code = null, bool submit = false) => new { email = destination, nonce, code, previousEmail = submit ? previousEmail : null };
        using var requested = await AccountJourney.PostAsync(client, "/api/account-recovery/code", Body(), Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(app.Services, destination, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/account-recovery/verify", Body(code), Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(client, "/api/account-recovery", Body(submit: true), Ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
    }

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(new()
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task OccupyAsync(IServiceProvider services, Guid userId, LoginMethodType type, string destination)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var saved = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var identities = provider.GetRequiredService<IExternalLoginLock>();
            if (type == LoginMethodType.Phone) await identities.LockPhoneAsync(PhoneNumber.Create(destination).Value, ct);
            else await identities.LockEmailAsync(Email.Create(destination).Value, ct);
            var methods = provider.GetRequiredService<ILoginMethodRepository>();
            await methods.LockUserAsync(userId, ct);
            var method = type switch
            {
                LoginMethodType.Phone => LoginMethod.CreatePhone(userId, PhoneNumber.Create(destination).Value),
                LoginMethodType.Google => LoginMethod.CreateGoogle(userId, Guid.NewGuid().ToString("N"), Email.Create(destination).Value),
                _ => LoginMethod.CreateEmail(userId, Email.Create(destination).Value),
            };
            var linked = await provider.GetRequiredService<LoginMethodLinker>().VerifyAsync(method,
                provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, ct);
            if (linked.IsFailure) return linked;
            methods.Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }

    private static async Task<JsonElement> RowAsync(HttpClient client, string destination)
    {
        using var response = await client.GetAsync("/api/platform/recoveries/paged?search=" + Uri.EscapeDataString(destination), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return Assert.Single(json!.RootElement.GetProperty("items").EnumerateArray()).Clone();
    }

    private static async Task<Snapshot> SnapshotAsync(IServiceProvider services, Guid id, Guid userId, Guid otherId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recovery = await db.AccountRecoveryRequests.AsNoTracking().SingleAsync(item => item.Id == id, Ct);
        Assert.Equal(AccountRecoveryStatus.Pending, recovery.Status);
        Assert.Null(recovery.DecidedAtUtc);
        Assert.Null(recovery.DecidedByOperatorId);
        Assert.Null(recovery.DecisionReason);
        return new Snapshot(
            JsonSerializer.Serialize(recovery),
            JsonSerializer.Serialize(await db.LoginMethods.AsNoTracking().Where(item => item.UserId == userId || item.UserId == otherId).OrderBy(item => item.Id).ToArrayAsync(Ct)),
            JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(item => item.Id == userId || item.Id == otherId).OrderBy(item => item.Id).ToArrayAsync(Ct)),
            JsonSerializer.Serialize(await db.OutboxMessages.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(Ct)),
            JsonSerializer.Serialize(await db.SecurityEvents.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(Ct)),
            JsonSerializer.Serialize(await db.AccountRecoveryNotices.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(Ct)));
    }

    private sealed record Snapshot(string Recovery, string Methods, string Accounts, string Outbox, string Audit, string Notices);

}
