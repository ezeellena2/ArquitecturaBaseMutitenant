using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Valida el alta B2B real: la cookie de identidad crea organización y empresa sin abrir Personal.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class BusinessSignupTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Email_signup_creates_one_business_and_company_without_personal_and_replays_safely()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = $"business-signup-{Guid.NewGuid():N}@example.test";
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var requested = await PostAsync(client, "/api/auth/business-signup/code",
            new { email, acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await PostAsync(client, "/api/auth/business-signup/verify",
            new { email, code, acceptedTerms = true, culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires" });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        Assert.Contains(verified.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("Identity.Application", StringComparison.Ordinal));

        using var antiforgery = await client.GetAsync("/api/auth/external/google/antiforgery", Ct);
        Assert.Equal(HttpStatusCode.OK, antiforgery.StatusCode);
        using var tokenBody = await antiforgery.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var requestToken = tokenBody!.RootElement.GetProperty("requestToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(requestToken));

        var operationId = Guid.NewGuid();
        var content = JsonContent.Create(new
        {
            organizationName = "Organización E2E",
            slug = $"e2e-{Guid.NewGuid():N}"[..20],
            firstCompanyName = "Empresa E2E",
            taxId = (string?)null,
            acceptedTerms = true,
        });
        using var signup = new HttpRequestMessage(HttpMethod.Post, "/api/auth/business-signup") { Content = content };
        signup.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        signup.Headers.Add("RequestVerificationToken", requestToken);
        using var created = await client.SendAsync(signup, Ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var result = await created.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var tenantId = result!.RootElement.GetProperty("tenantId").GetGuid();
        var slug = result.RootElement.GetProperty("slug").GetString()!;
        Assert.Equal("Active", result.RootElement.GetProperty("status").GetString());

        using var replayContent = JsonContent.Create(new
        {
            organizationName = "Organización E2E",
            slug,
            firstCompanyName = "Empresa E2E",
            taxId = (string?)null,
            acceptedTerms = true,
        });
        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/business-signup")
        {
            Content = replayContent,
        };
        replay.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        replay.Headers.Add("RequestVerificationToken", requestToken);
        using var replayed = await client.SendAsync(replay, Ct);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Contains(replayed.Headers, header => header.Key == "Idempotent-Replayed");
        using var replayResult = await replayed.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(tenantId, replayResult!.RootElement.GetProperty("tenantId").GetGuid());

        await AssertBusinessRowsAsync(email, tenantId, slug);
    }

    [Fact]
    public async Task Organization_creation_requires_an_identity_cookie_and_antiforgery()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        using var noSession = new HttpRequestMessage(HttpMethod.Post, "/api/auth/business-signup")
        {
            Content = JsonContent.Create(new
            {
                organizationName = "Organización",
                slug = "org-sin-sesion",
                firstCompanyName = "Empresa",
                acceptedTerms = true,
            }),
        };
        noSession.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

        using var response = await client.SendAsync(noSession, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Validation.Failed", problem!.RootElement.GetProperty("code").GetString());
    }

    private async Task AssertBusinessRowsAsync(string email, Guid tenantId, string slug)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t."Kind", t."Status", t."Slug", c."Name", m."Status", access."RoleName",
                   (SELECT count(*) FROM platform."Tenants" personal
                    JOIN identity."UserTenantAccesses" pa ON pa."TenantId" = personal."Id"
                    WHERE pa."UserId" = u."Id" AND personal."Kind" = 'Personal')
            FROM identity."LoginMethods" lm
            JOIN identity."AspNetUsers" u ON u."Id" = lm."UserId"
            JOIN identity."UserTenantAccesses" access ON access."UserId" = u."Id"
            JOIN platform."Tenants" t ON t."Id" = access."TenantId"
            JOIN tenant."Members" m ON m."TenantId" = t."Id" AND m."UserId" = u."Id"
            JOIN tenant."Companies" c ON c."TenantId" = t."Id"
            WHERE lm."Value" = @email AND t."Id" = @tenantId
            """;
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal("Business", reader.GetString(0));
        Assert.Equal("Active", reader.GetString(1));
        Assert.Equal(slug, reader.GetString(2));
        Assert.Equal("Empresa E2E", reader.GetString(3));
        Assert.Equal("Active", reader.GetString(4));
        Assert.Equal("TenantAdmin", reader.GetString(5));
        Assert.Equal(0L, reader.GetInt64(6));
        Assert.False(await reader.ReadAsync(Ct));
        await reader.DisposeAsync();

        await using var membership = connection.CreateCommand();
        membership.CommandText = """
            SELECT count(*)
            FROM tenant."CompanyMemberships" cm
            JOIN tenant."RoleAssignments" assignment
              ON assignment."TenantId" = cm."TenantId"
             AND assignment."CompanyId" = cm."CompanyId"
             AND assignment."MemberId" = cm."MemberId"
            JOIN tenant."Roles" role
              ON role."TenantId" = assignment."TenantId"
             AND role."Id" = assignment."RoleId"
            WHERE cm."TenantId" = @tenantId
              AND cm."Status" = 'Active'
              AND role."Name" = 'CompanyAdmin'
            """;
        membership.Parameters.AddWithValue("tenantId", tenantId);
        Assert.Equal(1L, (long)(await membership.ExecuteScalarAsync(Ct))!);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }
}
