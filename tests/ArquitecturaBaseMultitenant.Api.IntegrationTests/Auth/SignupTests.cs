using System.Net;
using System.Net.Http.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class SignupTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Anonymous_signup_does_not_verify_a_method_added_by_another_account()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create("pending-signup-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        var userId = Guid.Empty;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                userId = (await services.GetRequiredService<IUserRepository>()
                    .CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
                services.GetRequiredService<ILoginMethodRepository>().Add(LoginMethod.CreateEmail(userId, email));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var client = factory.CreateClient();
        using var requested = await client.PostAsJsonAsync("/test/auth/signup", new { email = email.Value, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(email.Value);
        using var verified = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = email.Value, code, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, verified.StatusCode);
        Assert.False(verified.Headers.Contains("Set-Cookie"));
        await using var checkScope = factory.Services.CreateAsyncScope();
        var lookup = checkScope.ServiceProvider.GetRequiredService<IUserLookup>();
        Assert.Null(await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, email.Value, Ct));
        Assert.Equal(userId, (await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, Ct))!.UserId);
    }

    [Fact]
    public async Task Pickup_signup_creates_one_identity_verified_method_personal_space_and_both_acceptances()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"signup-{Guid.NewGuid():N}@example.test";

        var requested = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true, culture = "en-US", timeZoneId = "America/New_York" }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var code = await ReadPickupCodeAsync(address);
        var verified = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code, acceptedTerms = true, culture = "en-US", timeZoneId = "America/New_York" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"),
            value => value.Contains("Identity.Application", StringComparison.Ordinal));

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using (var lookup = connection.CreateCommand())
        {
            lookup.CommandText = """
                SELECT a."TenantId"
                FROM identity."UserTenantAccesses" a
                JOIN identity."LoginMethods" lm ON lm."UserId" = a."UserId"
                WHERE lm."Value" = @email
                """;
            lookup.Parameters.AddWithValue("email", address);
            var tenantId = (Guid)(await lookup.ExecuteScalarAsync(Ct)
                ?? throw new Xunit.Sdk.XunitException("El índice no contiene el espacio personal."));
            await using var transaction = await connection.BeginTransactionAsync(Ct);
            await using var setTenant = connection.CreateCommand();
            setTenant.Transaction = transaction;
            setTenant.CommandText = "SELECT set_config('app.tenant_id', @tenant, true)";
            setTenant.Parameters.AddWithValue("tenant", tenantId.ToString());
            await setTenant.ExecuteNonQueryAsync(Ct);

            await AssertSignupRowsAsync(connection, transaction, address);
            await transaction.RollbackAsync(Ct);
        }
    }

    private static async Task AssertSignupRowsAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, string address)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT count(DISTINCT u."Id"), count(DISTINCT lm."Id"),
                   count(DISTINCT t."Id"), count(DISTINCT m."Id"),
                   count(DISTINCT s."Id"), count(DISTINCT la."Id")
            FROM identity."AspNetUsers" u
            JOIN identity."LoginMethods" lm ON lm."UserId" = u."Id"
            JOIN tenant."Members" m ON m."UserId" = u."Id"
            JOIN platform."Tenants" t ON t."Id" = m."TenantId"
            JOIN tenant."TenantSettings" s ON s."TenantId" = t."Id"
            JOIN identity."LegalAcceptances" la ON la."UserId" = u."Id"
            WHERE lm."Value" = @email AND lm."VerifiedAtUtc" IS NOT NULL
              AND t."Kind" = 'Personal' AND m."Status" = 'Active'
              AND u."Culture" = 'en-US'
              AND u."TimeZoneId" = 'America/Argentina/Buenos_Aires'
              AND s."DefaultCurrency" = 'ARS'
            """;
        command.Parameters.AddWithValue("email", address);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(1, reader.GetInt64(1));
        Assert.Equal(1, reader.GetInt64(2));
        Assert.Equal(1, reader.GetInt64(3));
        Assert.Equal(1, reader.GetInt64(4));
        Assert.Equal(2, reader.GetInt64(5));
    }

    [Fact]
    public async Task Existing_email_with_different_case_enters_without_creating_second_account()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"existing-signup-{Guid.NewGuid():N}@example.test";
        var requested = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(address);
        var first = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        await using (var agingConnection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await agingConnection.OpenAsync(Ct);
            await using var age = agingConnection.CreateCommand();
            age.CommandText = """
                UPDATE identity."LoginCodes"
                SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
                WHERE "Destination" = @email
                """;
            age.Parameters.AddWithValue("email", address);
            await age.ExecuteNonQueryAsync(Ct);
        }

        var upperCaseAddress = address.ToUpperInvariant();
        var again = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = upperCaseAddress, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        var secondCode = await ReadPickupCodeAsync(address);
        var second = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = upperCaseAddress, code = secondCode, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(DISTINCT u."Id"), count(DISTINCT la."Id")
            FROM identity."AspNetUsers" u
            JOIN identity."LoginMethods" lm ON lm."UserId" = u."Id"
            JOIN identity."LegalAcceptances" la ON la."UserId" = u."Id"
            WHERE lm."Value" = @email
            """;
        command.Parameters.AddWithValue("email", address);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(2, reader.GetInt64(1));
    }

    [Fact]
    public async Task Existing_email_signup_failures_lock_the_account_and_block_a_correct_code()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"signup-lockout-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(address);

        for (var batch = 0; batch < 2; batch++)
        {
            if (batch > 0) await AgeLoginCodesAsync(address);
            var requested = await client.PostAsJsonAsync("/test/auth/signup",
                new { email = address, acceptedTerms = true }, Ct);
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var correctCode = await ReadPickupCodeAsync(address);
            var wrongCode = correctCode == "000000" ? "999999" : "000000";
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var failed = await client.PostAsJsonAsync("/test/auth/signup/verify",
                    new { email = address, code = wrongCode, acceptedTerms = true }, Ct);
                Assert.False(failed.Headers.Contains("Set-Cookie"));
                Assert.NotEqual(HttpStatusCode.NoContent, failed.StatusCode);
            }
        }

        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISignInService>()
                .IsLockedOutAsync(userId, Ct));

        await AgeLoginCodesAsync(address);
        var finalRequest = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, finalRequest.StatusCode);
        var finalCode = await ReadPickupCodeAsync(address);
        var blocked = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code = finalCode, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        var problem = await blocked.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        Assert.Equal("Identity.Account.LockedOut", problem.GetProperty("code").GetString());
        Assert.False(blocked.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Existing_email_locked_by_login_cannot_enter_through_signup()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"signup-bypass-{Guid.NewGuid():N}@example.test";
        var userId = await CreateVerifiedEmailAccountAsync(address);
        var requested = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await ReadPickupCodeAsync(address);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var signIn = services.GetRequiredService<ISignInService>();
                for (var attempt = 0; attempt < 10; attempt++)
                    await signIn.RegisterFailedAttemptAsync(userId, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var blocked = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.False(blocked.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Closed_signup_does_not_reveal_account_existence_without_a_valid_code()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var known = $"closed-known-{Guid.NewGuid():N}@example.test";
        var unknown = $"closed-unknown-{Guid.NewGuid():N}@example.test";
        await CreateVerifiedEmailAccountAsync(known);
        await SetConsumerSignupAsync("Closed");
        try
        {
            var knownResponse = await client.PostAsJsonAsync("/test/auth/signup/verify",
                new { email = known, code = "000000", acceptedTerms = true }, Ct);
            var unknownResponse = await client.PostAsJsonAsync("/test/auth/signup/verify",
                new { email = unknown, code = "000000", acceptedTerms = true }, Ct);

            Assert.Equal(HttpStatusCode.BadRequest, knownResponse.StatusCode);
            Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
            var knownProblem = await knownResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            var unknownProblem = await unknownResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            Assert.Equal(LoginCodeErrors.InvalidCode, knownProblem.GetProperty("code").GetString());
            Assert.Equal(knownProblem.GetProperty("code").GetString(), unknownProblem.GetProperty("code").GetString());
        }
        finally
        {
            await SetConsumerSignupAsync("Open");
        }
    }

    [Fact]
    public async Task Suspended_account_is_not_disclosed_by_invalid_signup_code()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient();
        var address = $"suspended-signup-{Guid.NewGuid():N}@example.test";
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

        var response = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code = "000000", acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
        Assert.Equal(LoginCodeErrors.InvalidCode, problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Closed_personal_signup_rejects_the_request_without_sending_a_code()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var address = $"closed-signup-{Guid.NewGuid():N}@example.test";
        await SetConsumerSignupAsync("Closed");
        try
        {
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/test/auth/signup",
                new { email = address, acceptedTerms = true }, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            Assert.Equal("Auth.Signup.Closed", problem.GetProperty("code").GetString());

            await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM identity.\"LoginCodes\" WHERE \"Destination\" = @email";
            command.Parameters.AddWithValue("email", address);
            Assert.Equal(0L, await command.ExecuteScalarAsync(Ct));
        }
        finally
        {
            await SetConsumerSignupAsync("Open");
        }
    }

    private async Task<Guid> CreateVerifiedEmailAccountAsync(string address)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = Email.Create(address).Value;
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        Guid userId = Guid.Empty;

        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            userId = (await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            var method = LoginMethod.CreateEmail(userId, email);
            method.Verify(nowUtc);
            method.MakePrimary();
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            await users.SetPrimaryEmailAsync(userId, email, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);

        return userId;
    }

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

    private async Task SetConsumerSignupAsync(string mode)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE platform."PlatformSettings" SET "ConsumerSignup" = @mode
            WHERE "Id" = '00000000-0000-0000-0000-000000000001'
            """;
        command.Parameters.AddWithValue("mode", mode);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
        await factory.Services.GetRequiredService<HybridCache>().RemoveAsync("p:settings", Ct);
    }

    private Task<string> ReadPickupCodeAsync(string email) =>
        PickupCodeReader.ReadAsync(factory.Services, email, Ct);
}
