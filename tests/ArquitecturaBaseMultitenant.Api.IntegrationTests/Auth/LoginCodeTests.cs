using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>
/// Comprueba el ingreso por códigos leídos del pickup de correo. Exige un método verificado y un código de
/// uso único, y rechaza acceso empresa sin membresía.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Invitation_login_emits_only_identity_cookie_without_personal_space()
    {
        using var client = factory.CreateClient();
        var email = $"invitation-login-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(email);
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(email);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            value => value.Contains("Identity.Application", StringComparison.Ordinal));
        using var body = await verified.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(Ct);
        Assert.Equal("/invitacion", body!.RootElement.GetProperty("returnUrl").GetString());
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct));
        using var replay = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/invitacion" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Known_email_emits_a_code_and_unknown_email_returns_its_field_error()
    {
        using var client = factory.CreateClient();
        var known = $"login-{Guid.NewGuid():N}@example.test";
        var unknown = $"unknown-{Guid.NewGuid():N}@example.test";
        await CreateVerifiedEmailAccountAsync(known);

        var knownResponse = await client.PostAsJsonAsync("/test/auth/request-code", new { email = known }, Ct);
        var unknownResponse = await client.PostAsJsonAsync("/test/auth/request-code", new { email = unknown }, Ct);

        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownResponse.StatusCode);
        var problem = await unknownResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        Assert.Equal("Auth.LoginCode.AccountNotFound", problem.GetProperty("code").GetString());
        Assert.Equal("No hay una cuenta con ese correo.", problem.GetProperty("errors").GetProperty("email")[0].GetString());
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
    public async Task Inactive_managed_member_cannot_use_the_email_to_sign_into_personal_access()
    {
        var email = Email.Create($"managed-ex-member-{Guid.NewGuid():N}@example.test").Value;
        using var signupClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var memberAccount = await AccountJourney.RegisterAsync(factory, signupClient, Ct, email);
        var administratorId = await CreateVerifiedEmailAccountAsync($"managed-admin-{Guid.NewGuid():N}@example.test");
        var tenantId = await CreateBusinessOrganizationAsync(memberAccount.UserId, administratorId);
        await AssignManagedEmailAsync(memberAccount.UserId, tenantId);

        await AgeLoginCodesAsync(email.Value);
        using var loginClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var requested = await AccountJourney.PostAsync(loginClient, "/api/auth/login-code", new { email = email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(email.Value);

        await DeactivateOrganizationMemberAsync(memberAccount.UserId, tenantId);
        using var rejected = await AccountJourney.PostAsync(loginClient, "/api/auth/login-code/verify",
            new { email = email.Value, code, returnUrl = "/connect/authorize?client_id=web&access=consumer" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var problem = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(Ct);
        Assert.Equal(LoginCodeErrors.InvalidCode, problem!.RootElement.GetProperty("code").GetString());
        Assert.False(rejected.Headers.Contains("Set-Cookie"));

        await AgeLoginCodesAsync(email.Value);
        using var withheld = await AccountJourney.PostAsync(loginClient, "/api/auth/login-code",
            new { email = email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, withheld.StatusCode);
        await using var codeScope = factory.Services.CreateAsyncScope();
        var destination = LoginCodeDestination.ForEmail(email);
        var latestCode = await codeScope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .ExecuteInTransactionAsync(async ct => Result.Success(await codeScope.ServiceProvider
                .GetRequiredService<ILoginCodeRepository>()
                .GetLatestAsync(destination, LoginCodePurpose.Login, requestedByUserId: null, ct)),
                CommitPolicy.OnSuccess, Ct);
        Assert.NotNull(latestCode.Value);
        Assert.Null(latestCode.Value.SentAtUtc);
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

    private Task<string> ReadPickupCodeAsync(string email) =>
        PickupCodeReader.ReadAsync(factory.Services, email, Ct);

    private async Task<Guid> CreateBusinessOrganizationAsync(Guid memberUserId, Guid ownerUserId)
    {
        var tenant = Tenant.CreateBusiness("Dominio administrado HTTP", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"),
                [memberUserId, ownerUserId], ownerUserId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private async Task AssignManagedEmailAsync(Guid userId, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var method = Assert.Single(await services.GetRequiredService<ILoginMethodRepository>()
                .ListByUserIdAsync(userId, ct));
            method.AssignManagedByOrganization(tenantId);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task DeactivateOrganizationMemberAsync(Guid userId, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var member = await services.GetRequiredService<IMemberRepository>().GetByUserIdAsync(userId, ct);
            Assert.NotNull(member);
            Assert.True(member.Deactivate().IsSuccess);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
}
