using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeKit;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Known_and_unknown_email_get_the_same_request_response()
    {
        using var client = factory.CreateClient();
        var known = $"login-{Guid.NewGuid():N}@example.test";
        var unknown = $"unknown-{Guid.NewGuid():N}@example.test";
        await CreateVerifiedEmailAccountAsync(known);

        var knownResponse = await client.PostAsJsonAsync("/test/auth/request-code", new { email = known }, Ct);
        var unknownResponse = await client.PostAsJsonAsync("/test/auth/request-code", new { email = unknown }, Ct);

        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(await knownResponse.Content.ReadAsStringAsync(Ct),
            await unknownResponse.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Pickup_code_signs_in_only_verified_method_and_is_consumed_once()
    {
        using var client = factory.CreateClient();
        var email = $"pickup-login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(email);

        var requested = await client.PostAsJsonAsync("/test/auth/request-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);

        var code = await ReadPickupCodeAsync(email);
        var invalid = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email, code = "000000", returnUrl = "/connect/authorize?client_id=web" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var verified = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email, code, returnUrl = "/connect/authorize?client_id=web" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            value => value.Contains("Identity.Application", StringComparison.Ordinal));

        var replay = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email, code, returnUrl = "/connect/authorize?client_id=web" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var audits = scope.ServiceProvider.GetRequiredService<ILoginAuditRepository>();
        Assert.NotNull(await audits.FindLastSuccessAtUtcAsync(userId, Ct));
    }

    [Fact]
    public async Task A_code_for_an_unverified_method_cannot_sign_in()
    {
        using var client = factory.CreateClient();
        var address = $"unverified-{Guid.NewGuid():N}@example.test";
        await CreateEmailAccountAsync(address, verified: false);
        const string code = "123456";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var destination = LoginCodeDestination.ForEmail(Email.Create(address).Value);
            var hash = services.GetRequiredService<ILoginCodeHasher>()
                .Hash(destination, LoginCodePurpose.Login, code);
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<ILoginCodeRepository>().Add(LoginCode.Issue(destination,
                    LoginCodePurpose.Login, null, hash, nowUtc, TimeSpan.FromMinutes(10), maxAttempts: 5));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        var response = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email = address, code, returnUrl = "/connect/authorize?client_id=web" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Business_login_without_membership_rejects_before_issuing_a_cookie()
    {
        using var client = factory.CreateClient();
        var email = $"business-gate-{Guid.NewGuid():N}@example.test";
        await CreateVerifiedEmailAccountAsync(email);

        var requested = await client.PostAsJsonAsync("/test/auth/request-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var code = await ReadPickupCodeAsync(email);

        var rejected = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email, code, returnUrl = "/connect/authorize?client_id=web&access=business" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Contains(AccessErrors.NotMemberCode, await rejected.Content.ReadAsStringAsync(Ct),
            StringComparison.Ordinal);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));

        var replay = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email, code, returnUrl = "/connect/authorize?client_id=web&access=consumer" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Repeated_invalid_codes_lock_the_Identity_account()
    {
        using var client = factory.CreateClient();
        var address = $"lockout-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(address);
        var request = await client.PostAsJsonAsync("/test/auth/request-code", new { email = address }, Ct);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var firstCode = await ReadPickupCodeAsync(address);
        var firstInvalid = firstCode == "000000" ? "999999" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rejected = await client.PostAsJsonAsync("/test/auth/verify-code",
                new { email = address, code = firstInvalid, returnUrl = "/connect/authorize?client_id=web" }, Ct);
            Assert.NotEqual(HttpStatusCode.OK, rejected.StatusCode);
        }

        await AgeLoginCodesAsync(address);
        var secondRequest = await client.PostAsJsonAsync("/test/auth/request-code", new { email = address }, Ct);
        Assert.Equal(HttpStatusCode.OK, secondRequest.StatusCode);
        var secondCode = await ReadPickupCodeAsync(address);
        var secondInvalid = secondCode == "000000" ? "999999" : "000000";
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var rejected = await client.PostAsJsonAsync("/test/auth/verify-code",
                new { email = address, code = secondInvalid, returnUrl = "/connect/authorize?client_id=web" }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        var locked = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email = address, code = secondInvalid, returnUrl = "/connect/authorize?client_id=web" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISignInService>().IsLockedOutAsync(userId, Ct));
    }

    [Fact]
    public async Task Suspended_account_receives_code_and_is_rejected_after_correct_verification()
    {
        using var client = factory.CreateClient();
        var address = $"suspended-login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(address);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await manager.FindByIdAsync(userId.ToString("D"));
                Assert.NotNull(user);
                Assert.True(user.Suspend().IsSuccess);
                Assert.True((await manager.UpdateAsync(user)).Succeeded);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var requested = await client.PostAsJsonAsync("/test/auth/request-code", new { email = address }, Ct);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var code = await ReadPickupCodeAsync(address);
        var rejected = await client.PostAsJsonAsync("/test/auth/verify-code",
            new { email = address, code, returnUrl = "/connect/authorize?client_id=web" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Contains("Identity.Account.Suspended", await rejected.Content.ReadAsStringAsync(Ct),
            StringComparison.Ordinal);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Verifying_without_a_requested_code_does_not_lock_or_disclose_an_account()
    {
        using var client = factory.CreateClient();
        var known = $"no-code-known-{Guid.NewGuid():N}@example.test";
        var unknown = $"no-code-unknown-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(known);
        const string returnUrl = "/connect/authorize?client_id=web";

        for (var attempt = 0; attempt < 11; attempt++)
        {
            var knownResponse = await client.PostAsJsonAsync("/test/auth/verify-code",
                new { email = known, code = "000000", returnUrl }, Ct);
            var unknownResponse = await client.PostAsJsonAsync("/test/auth/verify-code",
                new { email = unknown, code = "000000", returnUrl }, Ct);

            Assert.Equal(HttpStatusCode.BadRequest, knownResponse.StatusCode);
            Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
            var knownProblem = await knownResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            var unknownProblem = await unknownResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            Assert.Equal(LoginCodeErrors.InvalidCode, knownProblem.GetProperty("code").GetString());
            Assert.Equal(knownProblem.GetProperty("code").GetString(), unknownProblem.GetProperty("code").GetString());
        }

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ISignInService>()
            .IsLockedOutAsync(userId, Ct));
    }

    private Task<Guid> CreateVerifiedEmailAccountAsync(string address) =>
        CreateEmailAccountAsync(address, verified: true);

    private async Task AgeLoginCodesAsync(string address)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE identity."LoginCodes"
            SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
            WHERE "Destination" = @email
            """;
        command.Parameters.AddWithValue("email", address);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<Guid> CreateEmailAccountAsync(string address, bool verified)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var users = services.GetRequiredService<IUserRepository>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var email = Email.Create(address).Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        Guid userId = Guid.Empty;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreateEmail(userId, email);
            if (verified)
            {
                method.Verify(nowUtc);
                method.MakePrimary();
                await users.SetPrimaryEmailAsync(userId, email, ct);
            }
            methods.Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return userId;
    }

    private async Task<string> ReadPickupCodeAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var directory = Path.Combine(services.GetRequiredService<IHostEnvironment>().ContentRootPath, ".emails");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await services.GetRequiredService<IOutboxDispatchService>().DispatchOnceAsync(
                services.GetServices<IChannelSender>().ToArray(), Ct);
            foreach (var path in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.eml") : [])
            {
                MimeMessage message;
                await using (var stream = File.OpenRead(path))
                {
                    message = await MimeMessage.LoadAsync(stream, Ct);
                }
                if (!message.To.Mailboxes.Any(mailbox => mailbox.Address == email)) continue;
                var match = Regex.Match(message.TextBody ?? string.Empty, @"(?<!\d)\d{6}(?!\d)");
                Assert.True(match.Success, "El correo pickup no contiene un código de seis dígitos.");
                File.Delete(path);
                return match.Value;
            }
            await Task.Delay(100, Ct);
        }
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup de ingreso.");
    }
}
