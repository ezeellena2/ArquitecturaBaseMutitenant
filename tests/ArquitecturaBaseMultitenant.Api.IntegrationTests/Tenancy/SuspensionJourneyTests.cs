using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

[Collection(ApiTestGroup.Name)]
public sealed class SuspensionJourneyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Suspended_business_rejects_its_route_and_refresh_but_keeps_me_available()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var email = $"ana-suspension-{Guid.NewGuid():N}@example.test";
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Email:Delivery", "PickupDirectory");
            builder.UseSetting("Authentication:Google:ClientId", "");
            builder.UseSetting("Seed:Development:AnaEmail", email);
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        const string verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizePath = "/connect/authorize?client_id=web&response_type=code"
            + "&redirect_uri=https%3A%2F%2Flocalhost%3A5174%2Fauth%2Fcallback"
            + "&scope=openid%20profile%20email%20offline_access%20api"
            + "&code_challenge=" + challenge + "&code_challenge_method=S256&access=business";

        using (var requested = await PostOnceAsync(client, "/api/auth/login-code", new { email }))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, email, Ct);
        using (var verified = await PostOnceAsync(client, "/api/auth/login-code/verify",
                   new { email, code, returnUrl = authorizePath }))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        using var authorized = await client.GetAsync(authorizePath, Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorized.StatusCode);
        var authorizationCode = QueryHelpers.ParseQuery(authorized.Headers.Location!.Query)["code"].ToString();
        Assert.NotEmpty(authorizationCode);
        using var exchanged = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["client_id"] = "web",
                ["redirect_uri"] = "https://localhost:5174/auth/callback",
                ["code_verifier"] = verifier,
            }), Ct);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        using var tokenBody = (await exchanged.Content.ReadFromJsonAsync<JsonDocument>(Ct))!;
        var accessToken = tokenBody.RootElement.GetProperty("access_token").GetString()!;
        var refreshToken = tokenBody.RootElement.GetProperty("refresh_token").GetString()!;
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var me = await client.SendAsync(meRequest, Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meBody = (await me.Content.ReadFromJsonAsync<JsonDocument>(Ct))!;
        var userId = meBody.RootElement.GetProperty("id").GetGuid();
        var tenantId = meBody.RootElement.GetProperty("activeTenantId").GetGuid();
        Assert.Equal("Empresa A", Assert.Single(
            meBody.RootElement.GetProperty("organizations").EnumerateArray()).GetProperty("name").GetString());

        await using (var owner = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await owner.OpenAsync(Ct);
            await using var suspend = new NpgsqlCommand("""
                UPDATE platform."Tenants" SET "Status" = 'Suspended' WHERE "Id" = @tenant_id
                """, owner);
            suspend.Parameters.AddWithValue("tenant_id", tenantId);
            Assert.Equal(1, await suspend.ExecuteNonQueryAsync(Ct));
        }
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ITenantStatusCache>().InvalidateAsync(tenantId, Ct);

        using var protectedRequest = new HttpRequestMessage(HttpMethod.Post, "/test/access/business-signup");
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var blocked = await client.SendAsync(protectedRequest, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        using var blockedBody = (await blocked.Content.ReadFromJsonAsync<JsonDocument>(Ct))!;
        Assert.Equal(TenantErrors.SuspendedCode, blockedBody.RootElement.GetProperty("code").GetString());

        using var recoveryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        recoveryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var recovery = await client.SendAsync(recoveryRequest, Ct);
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);

        // El mismo token ejercita las otras dos capas de estado con sus lectores y cachés reales.
        await using (var owner = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await owner.OpenAsync(Ct);
            await using var reactivate = new NpgsqlCommand("""
                UPDATE platform."Tenants" SET "Status" = 'Active' WHERE "Id" = @tenant_id
                """, owner);
            reactivate.Parameters.AddWithValue("tenant_id", tenantId);
            Assert.Equal(1, await reactivate.ExecuteNonQueryAsync(Ct));
            await using var setTenant = new NpgsqlCommand(
                "SELECT set_config('app.tenant_id', @tenant_id, false)", owner);
            setTenant.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
            await setTenant.ExecuteNonQueryAsync(Ct);
            await using var deactivate = new NpgsqlCommand("""
                UPDATE tenant."Members" SET "Status" = 'Inactive'
                WHERE "TenantId" = @tenant_id AND "UserId" = @user_id
                """, owner);
            deactivate.Parameters.AddWithValue("tenant_id", tenantId);
            deactivate.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await deactivate.ExecuteNonQueryAsync(Ct));
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITenantStatusCache>().InvalidateAsync(tenantId, Ct);
            await scope.ServiceProvider.GetRequiredService<IAccessStatusCache>()
                .InvalidateMemberAsync(userId, tenantId, Ct);
        }
        using var inactiveMemberRequest = new HttpRequestMessage(
            HttpMethod.Post, "/test/access/business-signup");
        inactiveMemberRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var inactiveMember = await client.SendAsync(inactiveMemberRequest, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, inactiveMember.StatusCode);

        await using (var owner = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await owner.OpenAsync(Ct);
            await using var suspendUser = new NpgsqlCommand("""
                UPDATE identity."AspNetUsers" SET "Status" = 'Suspended' WHERE "Id" = @user_id
                """, owner);
            suspendUser.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await suspendUser.ExecuteNonQueryAsync(Ct));
        }
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IAccessStatusCache>()
                .InvalidateUserAsync(userId, Ct);
        using var suspendedUserRequest = new HttpRequestMessage(
            HttpMethod.Post, "/test/access/business-signup");
        suspendedUserRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var suspendedUser = await client.SendAsync(suspendedUserRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, suspendedUser.StatusCode);

        await using (var owner = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await owner.OpenAsync(Ct);
            await using var activateUser = new NpgsqlCommand("""
                UPDATE identity."AspNetUsers" SET "Status" = 'Active' WHERE "Id" = @user_id
                """, owner);
            activateUser.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await activateUser.ExecuteNonQueryAsync(Ct));
            await using var setTenant = new NpgsqlCommand(
                "SELECT set_config('app.tenant_id', @tenant_id, false)", owner);
            setTenant.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
            await setTenant.ExecuteNonQueryAsync(Ct);
            await using var activateMember = new NpgsqlCommand("""
                UPDATE tenant."Members" SET "Status" = 'Active'
                WHERE "TenantId" = @tenant_id AND "UserId" = @user_id
                """, owner);
            activateMember.Parameters.AddWithValue("tenant_id", tenantId);
            activateMember.Parameters.AddWithValue("user_id", userId);
            Assert.Equal(1, await activateMember.ExecuteNonQueryAsync(Ct));
            await using var suspendTenant = new NpgsqlCommand("""
                UPDATE platform."Tenants" SET "Status" = 'Suspended' WHERE "Id" = @tenant_id
                """, owner);
            suspendTenant.Parameters.AddWithValue("tenant_id", tenantId);
            Assert.Equal(1, await suspendTenant.ExecuteNonQueryAsync(Ct));
        }
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<IAccessStatusCache>();
            await cache.InvalidateUserAsync(userId, Ct);
            await cache.InvalidateMemberAsync(userId, tenantId, Ct);
            await scope.ServiceProvider.GetRequiredService<ITenantStatusCache>().InvalidateAsync(tenantId, Ct);
        }
        using var refresh = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = "web",
            }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostOnceAsync(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        return await client.SendAsync(request, Ct);
    }
}
