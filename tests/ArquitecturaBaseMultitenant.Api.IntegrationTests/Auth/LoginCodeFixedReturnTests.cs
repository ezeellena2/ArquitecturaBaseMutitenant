using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Prueba el cuerpo HTTP literal y la continuación fija antes de una autorización nueva.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeFixedReturnTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("none", "Tenancy.Access.NotMember", "es")]
    [InlineData("none", "Tenancy.Access.NotMember", "en")]
    [InlineData("inactive", "Tenancy.Member.Inactive", "es")]
    [InlineData("inactive", "Tenancy.Member.Inactive", "en")]
    [InlineData("pending", "Tenancy.Tenant.PendingApproval", "es")]
    [InlineData("pending", "Tenancy.Tenant.PendingApproval", "en")]
    [InlineData("provisioning", "Tenancy.Tenant.Provisioning", "es")]
    [InlineData("provisioning", "Tenancy.Tenant.Provisioning", "en")]
    [InlineData("suspended", "Tenancy.Tenant.Suspended", "es")]
    [InlineData("suspended", "Tenancy.Tenant.Suspended", "en")]
    [InlineData("closed", "Tenancy.Tenant.Closed", "es")]
    [InlineData("closed", "Tenancy.Tenant.Closed", "en")]
    public async Task Business_otp_rejections_match_the_documented_status_localization_and_metadata(string state, string error, string language)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        Guid? tenantId = null;
        if (state != "none")
        {
            tenantId = await CreateBusinessAsync(userId, state switch
            {
                "pending" => TenantStatus.PendingApproval,
                "provisioning" => TenantStatus.Provisioning,
                "suspended" => TenantStatus.Suspended,
                "closed" => TenantStatus.Closed,
                _ => TenantStatus.Active,
            });
            if (state == "inactive") await ChangeBusinessAsync(userId, tenantId.Value, state);
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var code = await RequestCodeAsync(client, email);
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/org", access = "business" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        Assert.False(denied.Headers.Contains("Retry-After"));
        var problem = (await denied.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        Assert.Equal(error, problem["code"]!.GetValue<string>());
        Assert.Equal(403, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
        Assert.Null(problem["errors"]);
        Assert.Null(problem["retryAfter"]);
        if (tenantId is { } organizationId)
        {
            Assert.Equal(organizationId.ToString("D"), problem["tenantId"]!.GetValue<string>());
            Assert.Equal("Empresa OTP fijo", problem["organizationName"]!.GetValue<string>());
        }
        else
        {
            Assert.Null(problem["tenantId"]);
            Assert.Null(problem["organizationName"]);
        }

        await using var development = factory.WithWebHostBuilder(builder => builder.UseDevelopmentTestOwner());
        using var documentationClient = development.CreateClient();
        using var documentResponse = await documentationClient.GetAsync("/openapi/v1.json", Ct);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        var document = (await documentResponse.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        var operation = document["paths"]!["/api/auth/login-code/verify"]!["post"]!;
        var documented = Assert.Single(operation["x-error-codes"]!.AsArray(), value => value?["code"]?.GetValue<string>() == error);
        Assert.Equal(403, documented!["status"]!.GetValue<int>());
        Assert.Null(documented["field"]);
        Assert.Equal(problem["detail"]!.GetValue<string>(), documented[language]!.GetValue<string>());
        var example = operation["responses"]!["403"]!["content"]!["application/problem+json"]!["examples"]![error + "." + language]!["value"]!;
        foreach (var property in new[] { "status", "code", "detail" })
            Assert.True(JsonNode.DeepEquals(problem[property], example[property]), property);
        foreach (var property in new[] { "errors", "retryAfter", "cancelTicket", "returnUrl" })
        {
            Assert.Null(problem[property]);
            Assert.Null(example[property]);
        }
        Assert.Equal(problem.ContainsKey("organizationName"), example.AsObject().ContainsKey("organizationName"));
        Assert.Equal(problem.ContainsKey("tenantId"), example.AsObject().ContainsKey("tenantId"));
        if (tenantId is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(example["organizationName"]!.GetValue<string>()));
            Assert.True(Guid.TryParseExact(example["tenantId"]!.GetValue<string>(), "D", out var exampleId));
            Assert.NotEqual(Guid.Empty, exampleId);
        }
    }

    [Theory]
    [InlineData("/", "consumer")]
    [InlineData("/org", "business")]
    public async Task Fixed_return_proves_identity_then_authorizes_only_the_selected_access(string returnUrl, string access)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        if (access == "business") await CreateBusinessAsync(userId);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var code = await RequestCodeAsync(client, email);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl, access }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.DoesNotContain(verified.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Platform", StringComparison.Ordinal));
        using var body = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(returnUrl, body!.RootElement.GetProperty("returnUrl").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.LegalAcceptances.AnyAsync(row => row.UserId == userId, Ct));
            Assert.DoesNotContain(await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct), row => row.Kind == TenantKind.Personal);
        }
        await AccountJourney.AuthorizeAsync(client, access, Ct);
        using var me = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = await me.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(userId, profile!.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(access, profile.RootElement.GetProperty("access").GetString());
        using var replay = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl, access }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("/", null)]
    [InlineData("/org", null)]
    [InlineData("/", "business")]
    [InlineData("/org", "consumer")]
    [InlineData("/", "platform")]
    [InlineData("/org", "platform")]
    [InlineData("/", "Consumer")]
    [InlineData("/", " consumer")]
    [InlineData("/", "consumer ")]
    [InlineData("/", "con\u200bsumer")]
    [InlineData("/", "con\u0001sumer")]
    [InlineData(" /", "consumer")]
    [InlineData("/org ", "business")]
    [InlineData("/ org", "business")]
    [InlineData("/o\u200brg", "business")]
    [InlineData("/\u0001org", "business")]
    [InlineData("/\u200b", "consumer")]
    [InlineData("/org\n", "business")]
    [InlineData("/org\t", "business")]
    [InlineData("/org?x=1", "business")]
    [InlineData("/org#fragment", "business")]
    [InlineData("/?x=1", "consumer")]
    [InlineData("/#fragment", "consumer")]
    [InlineData("/org/", "business")]
    [InlineData("//org", "business")]
    [InlineData("/\\org", "business")]
    [InlineData("https://localhost/org", "business")]
    [InlineData("/otra", "consumer")]
    [InlineData("/invitacion", "consumer")]
    [InlineData("/connect/authorize?access=consumer", "consumer")]
    [InlineData("/connect/authorize?access=business", "consumer")]
    public async Task Invalid_literal_body_does_not_consume_the_code(string returnUrl, string? access)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        var code = await RequestCodeAsync(client, email);
        using var rejected = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl, access }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
        using var problem = await rejected.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Validation.Failed", problem!.RootElement.GetProperty("code").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var challenge = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AsNoTracking()
                .SingleAsync(row => row.Destination == email && row.Purpose == LoginCodePurpose.Login, Ct);
            Assert.Null(challenge.ConsumedAtUtc);
            Assert.Equal(0, challenge.FailedAttempts);
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginAudits.AnyAsync(row => row.UserId == userId, Ct));
        }
        using var valid = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/", access = "consumer" }, Ct);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/org")]
    public async Task Missing_or_numeric_access_is_rejected_without_consuming_the_code(string returnUrl)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (_, email) = await CreateAccountAsync();
        using var client = factory.CreateClient();
        var code = await RequestCodeAsync(client, email);
        using var omitted = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, omitted.StatusCode);
        using var numeric = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl, access = 1 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);
        Assert.False(omitted.Headers.Contains("Set-Cookie"));
        Assert.False(numeric.Headers.Contains("Set-Cookie"));
        using var valid = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl = "/", access = "consumer" }, Ct);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Theory]
    [InlineData("consumer", "operator", "Tenancy.Access.PlatformOnly")]
    [InlineData("business", "operator", "Tenancy.Access.PlatformOnly")]
    [InlineData("consumer", "suspended", "Identity.Account.Suspended")]
    [InlineData("business", "suspended", "Identity.Account.Suspended")]
    [InlineData("consumer", "removed", "Auth.LoginCode.Invalid")]
    [InlineData("business", "removed", "Auth.LoginCode.Invalid")]
    public async Task Fixed_return_rechecks_account_and_method_before_cookie(string access, string change, string error)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        using var client = factory.CreateClient();
        var code = await RequestCodeAsync(client, email);
        await ChangeAccountAsync(userId, change);
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = access == "consumer" ? "/" : "/org", access }, Ct);
        Assert.Equal(change == "removed" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        using var problem = await denied.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(error, problem!.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("none", "Tenancy.Access.NotMember")]
    [InlineData("inactive", "Tenancy.Member.Inactive")]
    [InlineData("suspended", "Tenancy.Tenant.Suspended")]
    public async Task Fixed_business_return_requires_an_active_membership_and_organization(string state, string error)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        if (state != "none") await ChangeBusinessAsync(userId, await CreateBusinessAsync(userId), state);
        using var client = factory.CreateClient();
        var code = await RequestCodeAsync(client, email);
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl = "/org", access = "business" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        using var problem = await denied.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(error, problem!.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/", "consumer")]
    [InlineData("/org", "business")]
    public async Task Pending_deletion_binds_fixed_ticket_and_cancellation_restores_only_its_door(string returnUrl, string access)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        if (access == "business") await CreateBusinessAsync(userId);
        await ChangeAccountAsync(userId, "pending");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var code = await RequestCodeAsync(client, email);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl, access }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, verified.StatusCode);
        Assert.False(verified.Headers.Contains("Set-Cookie"));
        using var problem = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Identity.Account.PendingDeletion", problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(returnUrl, problem.RootElement.GetProperty("returnUrl").GetString());
        var secret = problem.RootElement.GetProperty("cancelTicket").GetString()!;
        await AssertTicketAsync(userId, secret, returnUrl, consumed: false);
        using var cancelled = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel",
            new { cancelTicket = secret, returnUrl = access == "consumer" ? "/org" : "/", access = "platform", userId = Guid.NewGuid() }, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Contains(cancelled.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        Assert.DoesNotContain(cancelled.Headers.GetValues("Set-Cookie"), value => value.Contains("Identity.Platform", StringComparison.Ordinal));
        using var body = await cancelled.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(returnUrl, body!.RootElement.GetProperty("returnUrl").GetString());
        Assert.False(body.RootElement.TryGetProperty("userId", out _));
        await AssertTicketAsync(userId, secret, returnUrl, consumed: true);
        await AccountJourney.AuthorizeAsync(client, access, Ct);
        using var me = await client.GetAsync("/api/me", Ct);
        using var profile = await me.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(access, profile!.RootElement.GetProperty("access").GetString());
        using var replay = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("inactive", "Tenancy.Member.Inactive")]
    [InlineData("suspended", "Tenancy.Tenant.Suspended")]
    [InlineData("removed", "Tenancy.Access.NotMember")]
    public async Task Cancellation_rechecks_fixed_business_and_rolls_back_account_and_ticket(string state, string error)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, email) = await CreateAccountAsync();
        var tenantId = await CreateBusinessAsync(userId);
        await ChangeAccountAsync(userId, "pending");
        using var client = factory.CreateClient();
        var code = await RequestCodeAsync(client, email);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify", new { email, code, returnUrl = "/org", access = "business" }, Ct);
        using var proof = await verified.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var secret = proof!.RootElement.GetProperty("cancelTicket").GetString()!;
        await ChangeBusinessAsync(userId, tenantId, state);
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        using var problem = await denied.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(error, problem!.RootElement.GetProperty("code").GetString());
        await AssertTicketAsync(userId, secret, "/org", consumed: false);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(UserStatus.PendingDeletion, (await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(userId, Ct))!.Status);
    }

    [Theory]
    [InlineData("/", "expired", "Legal.AccountDeletion.CancelProofInvalid")]
    [InlineData("/org", "expired", "Legal.AccountDeletion.CancelProofInvalid")]
    [InlineData("/", "suspended", "Identity.Account.Suspended")]
    [InlineData("/org", "suspended", "Identity.Account.Suspended")]
    [InlineData("/", "operator", "Tenancy.Access.PlatformOnly")]
    [InlineData("/org", "operator", "Tenancy.Access.PlatformOnly")]
    public async Task Fixed_cancellation_rejects_expired_proof_or_changed_account_without_cookie(string returnUrl, string state, string error)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (userId, _) = await CreateAccountAsync();
        Guid? operatorId = null;
        if (state == "suspended")
        {
            await ChangeAccountAsync(userId, "suspended");
            var (createdOperator, _) = await CreateAccountAsync();
            operatorId = createdOperator;
            await ChangeAccountAsync(createdOperator, "operator");
        }
        else await ChangeAccountAsync(userId, "pending");
        string secret;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var secrets = services.GetRequiredService<ISecureTokenGenerator>();
            secret = secrets.Generate();
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                if (operatorId is { } id)
                    Assert.True((await services.GetRequiredService<IUserRepository>().RequestDeletionByOperatorAsync(userId, id, "Prueba suspendida", nowUtc, 30, ct)).IsSuccess);
                services.GetRequiredService<IAccountDeletionCancelTicketRepository>().Add(AccountDeletionCancelTicket.Issue(userId, secrets.Hash(secret), state == "expired" ? nowUtc.AddMinutes(-5) : nowUtc, returnUrl));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        if (state == "operator") await ChangeAccountAsync(userId, state);
        using var client = factory.CreateClient();
        using var denied = await AccountJourney.PostAsync(client, "/api/auth/deletion/cancel", new { cancelTicket = secret }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        using var problem = await denied.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(error, problem!.RootElement.GetProperty("code").GetString());
        await AssertTicketAsync(userId, secret, returnUrl, consumed: false);
    }

    [Fact]
    public async Task Fixed_verification_replay_has_no_cookie_and_changed_body_cannot_reuse_its_key()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var (_, email) = await CreateAccountAsync();
        using var client = factory.CreateClient();
        var code = await RequestCodeAsync(client, email);
        var key = Guid.NewGuid().ToString("D");
        var body = new { email, code, returnUrl = "/", access = "consumer" };
        using var verified = await VerifyWithKeyAsync(client, body, key);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        using var replay = await VerifyWithKeyAsync(client, body, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        Assert.False(replay.Headers.Contains("Set-Cookie"));
        using var changed = await VerifyWithKeyAsync(client, new { email, code, returnUrl = "/org", access = "business" }, key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
        Assert.False(changed.Headers.Contains("Set-Cookie"));
    }

    private static async Task<HttpResponseMessage> VerifyWithKeyAsync(HttpClient client, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login-code/verify") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private async Task AssertTicketAsync(Guid userId, string secret, string returnUrl, bool consumed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var hash = scope.ServiceProvider.GetRequiredService<ISecureTokenGenerator>().Hash(secret);
        var ticket = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AccountDeletionCancelTickets.AsNoTracking().SingleAsync(row => row.TokenHash == hash, Ct);
        Assert.Equal(userId, ticket.UserId);
        Assert.Equal(returnUrl, ticket.ReturnUrl);
        Assert.Equal(TimeSpan.FromMinutes(5), ticket.ExpiresAtUtc - ticket.IssuedAtUtc);
        Assert.Equal(consumed, ticket.ConsumedAtUtc is not null);
    }

    private async Task ChangeAccountAsync(Guid userId, string change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            if (change == "operator") await users.SetPlatformOperatorAsync(userId, true, ct);
            else if (change == "suspended") Assert.True((await users.SuspendAsync(userId, ct)).IsSuccess);
            else if (change == "pending") Assert.True((await users.RequestDeletionAsync(userId, "Prueba OTP fijo", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime, 30, ct)).IsSuccess);
            else
            {
                var methods = services.GetRequiredService<ILoginMethodRepository>();
                methods.Remove(Assert.Single(await methods.ListByUserIdAsync(userId, ct)));
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task ChangeBusinessAsync(Guid userId, Guid tenantId, string state)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            if (state == "suspended")
            {
                var tenant = await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenantId, ct);
                Assert.True(tenant!.Suspend().IsSuccess);
            }
            else
            {
                var member = await services.GetRequiredService<IMemberRepository>().GetByUserIdAsync(userId, ct);
                Assert.NotNull(member);
                Assert.True((state == "removed" ? member.Remove() : member.Deactivate()).IsSuccess);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<(Guid UserId, string Email)> CreateAccountAsync()
    {
        var email = Email.Create($"fixed-{Guid.NewGuid():N}@example.test").Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Guid id = default;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            id = (await services.GetRequiredService<IUserRepository>().CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreateEmail(id, email);
            method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            method.MakePrimary();
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await services.GetRequiredService<IUserRepository>().SetPrimaryEmailAsync(id, email, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return (id, email.Value);
    }

    private async Task<Guid> CreateBusinessAsync(Guid userId, TenantStatus status = TenantStatus.Active)
    {
        var (ownerId, _) = await CreateAccountAsync();
        var tenant = Tenant.CreateBusiness("Empresa OTP fijo", requiresApproval: status == TenantStatus.PendingApproval);
        if (status is TenantStatus.Active or TenantStatus.Suspended or TenantStatus.Closed)
            Assert.True(tenant.Activate().IsSuccess);
        if (status is TenantStatus.Suspended or TenantStatus.Closed)
            Assert.True(tenant.Suspend().IsSuccess);
        if (status == TenantStatus.Closed) Assert.True(tenant.Close().IsSuccess);
        Assert.Equal(status, tenant.Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId, ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private async Task<string> RequestCodeAsync(HttpClient client, string email)
    {
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        return await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
    }
}
