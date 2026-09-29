using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MimeKit;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class SignupTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
    public async Task Existing_email_enters_without_creating_second_account()
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

        var again = await client.PostAsJsonAsync("/test/auth/signup",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        var secondCode = await ReadPickupCodeAsync(address);
        var second = await client.PostAsJsonAsync("/test/auth/signup/verify",
            new { email = address, code = secondCode, acceptedTerms = true }, Ct);
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
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup de registro.");
    }
}
