using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformDomainAdministrationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Platform_verifies_and_removes_only_the_selected_domain_preserving_managed_methods()
    {
        var dns = new Dns();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDnsTxtLookup>();
            services.AddSingleton<IDnsTxtLookup>(dns);
        }));
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var platform = host.CreateClient(options);
        using var unauthorized = await platform.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/domains", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var domain = "corp-" + Guid.NewGuid().ToString("N") + ".example";
        using var business = host.CreateClient(options);
        var owner = await AccountJourney.RegisterAsync(factory, business, Ct, Email.Create("owner@" + domain).Value);
        var tenantId = await CreateBusinessAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(business, "business", Ct);
        using var registered = await AccountJourney.PostAsync(business, "/api/organization/settings/domains", new { domain }, Ct);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var untouchedDomain = "aside-" + Guid.NewGuid().ToString("N") + ".example";
        using var registeredAside = await AccountJourney.PostAsync(business, "/api/organization/settings/domains",
            new { domain = untouchedDomain }, Ct);
        Assert.Equal(HttpStatusCode.Created, registeredAside.StatusCode);
        using var denied = await business.GetAsync($"/api/platform/tenants/{tenantId:D}/domains", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var account = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await GrantAsync(account.UserId, PlatformOperators.All);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        var path = $"/api/platform/tenants/{tenantId:D}/domains";
        using var listed = await platform.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var json = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, json.RootElement.GetArrayLength());
        var row = Assert.Single(json.RootElement.EnumerateArray(), item => item.GetProperty("domain").GetString() == domain);
        Assert.Equal("Pending", row.GetProperty("status").GetString());
        var version = row.GetProperty("version").GetUInt32();
        var otherTenantId = await CreateBusinessAsync(owner.UserId);
        using var otherDomain = await PlatformJourney.PostAsync(platform,
            $"/api/platform/tenants/{otherTenantId:D}/domains/{domain}/verify", new { version, reason = "Verificar dominio ajeno" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, otherDomain.StatusCode);
        using var absentTenant = await platform.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/domains", Ct);
        Assert.Equal(HttpStatusCode.NotFound, absentTenant.StatusCode);
        // El espacio Personal no es una organización: 404 sin registrar una entrada a su alcance.
        var personalId = await PersonalSpaceAsync(owner.UserId);
        var personalPath = $"/api/platform/tenants/{personalId:D}/domains";
        using var personalList = await platform.GetAsync(personalPath, Ct);
        Assert.Equal(HttpStatusCode.NotFound, personalList.StatusCode);
        using var personalVerify = await PlatformJourney.PostAsync(platform, personalPath + "/" + domain + "/verify",
            new { version, reason = "Verificar dominio personal" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, personalVerify.StatusCode);
        using var personalRemove = await DeleteAsync(platform, personalPath + "/" + domain, version, "Retirar dominio personal");
        Assert.Equal(HttpStatusCode.NotFound, personalRemove.StatusCode);
        using var missing = await PlatformJourney.PostAsync(platform, path + "/absent.example/verify", new { version, reason = "Revisar DNS" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var invalid = await PlatformJourney.PostAsync(platform, path + "/" + domain + "/verify", new { version, reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var withoutVersion = await PlatformJourney.PostAsync(platform, path + "/" + domain + "/verify",
            new { reason = "Verificar dominio" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutVersion.StatusCode);
        using var noTxt = await PlatformJourney.PostAsync(platform, path + "/" + domain + "/verify", new { version, reason = "Verificar dominio" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, noTxt.StatusCode);
        dns.Value = row.GetProperty("txtRecordValue").GetString()!;
        var verifyKey = Guid.NewGuid().ToString("D");
        var verifyBody = new { version, reason = "Verificar dominio" };
        using var verified = await PostWithKeyAsync(platform, path + "/" + domain + "/verify", verifyBody, verifyKey);
        var savedVersion = await SavedVersion.ReadAsync(verified, Ct);
        Assert.NotEqual(version, savedVersion);
        using var verifiedReplay = await PostWithKeyAsync(platform, path + "/" + domain + "/verify", verifyBody, verifyKey);
        Assert.Equal(savedVersion, await SavedVersion.ReadAsync(verifiedReplay, Ct));
        Assert.Equal("true", Assert.Single(verifiedReplay.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(tenantId, await ManagedByAsync(owner.UserId, owner.Email.Value));

        // Unión de existentes y nuevos: un correo del dominio verificado después también queda administrado.
        using var colleagueClient = host.CreateClient(options);
        var colleague = await AccountJourney.RegisterAsync(factory, colleagueClient, Ct);
        using var consumerDenied = await colleagueClient.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumerDenied.StatusCode);
        var colleagueEmail = Email.Create("employee@" + domain).Value;
        await AddAndVerifyEmailAsync(colleagueClient, colleague, colleagueEmail);
        Assert.Equal(tenantId, await ManagedByAsync(colleague.UserId, colleagueEmail.Value));

        using var stale = await DeleteAsync(platform, path + "/" + domain, version, "Retirar dominio");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var current = await platform.GetAsync(path, Ct);
        using var currentJson = JsonDocument.Parse(await current.Content.ReadAsStringAsync(Ct));
        var verifiedRow = Assert.Single(currentJson.RootElement.EnumerateArray(),
            item => item.GetProperty("domain").GetString() == domain);
        Assert.Equal("Verified", verifiedRow.GetProperty("status").GetString());
        Assert.Equal(savedVersion, verifiedRow.GetProperty("version").GetUInt32());
        using var removed = await DeleteAsync(platform, path + "/" + domain, savedVersion, "Retirar dominio");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(tenantId, await ManagedByAsync(owner.UserId, owner.Email.Value));
        Assert.Equal(tenantId, await ManagedByAsync(colleague.UserId, colleagueEmail.Value));
        using var remaining = await platform.GetAsync(path, Ct);
        using var remainingJson = JsonDocument.Parse(await remaining.Content.ReadAsStringAsync(Ct));
        var aside = Assert.Single(remainingJson.RootElement.EnumerateArray());
        Assert.Equal(untouchedDomain, aside.GetProperty("domain").GetString());
        Assert.Equal("Pending", aside.GetProperty("status").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var events = await services.GetRequiredService<ApplicationDbContext>().SecurityEvents
                .Where(e => e.TargetTenantId == tenantId).ToArrayAsync(Ct);
            Assert.Single(events, e => e.Type.ToString() == "OrganizationDomainVerified");
            Assert.Single(events, e => e.Type.ToString() == "OrganizationDomainRemoved");
            Assert.False(await services.GetRequiredService<ApplicationDbContext>().SecurityEvents
                .AnyAsync(e => e.TargetTenantId == personalId, Ct));
            using var entered = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var member = await services.GetRequiredService<IMemberRepository>().GetByUserIdAsync(owner.UserId, ct);
                Assert.NotNull(member);
                Assert.True(member.Deactivate().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        using var cannotUseRemovedManagedEmail = host.CreateClient(options);
        await AgeLoginCodesAsync(owner.Email.Value);
        var outboxBefore = await OutboxCountAsync(owner.UserId);
        using var login = await AccountJourney.PostAsync(cannotUseRemovedManagedEmail, "/api/auth/login-code", new
        {
            email = owner.Email.Value,
        }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        Assert.Equal(outboxBefore, await OutboxCountAsync(owner.UserId));
        await GrantAsync(account.UserId, PlatformOperators.Reduced);
        using var supportDenied = await platform.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, supportDenied.StatusCode);
        using var unknownTenant = await platform.GetAsync($"/api/platform/tenants/{Guid.NewGuid():D}/domains", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, unknownTenant.StatusCode);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string path, uint version, string reason)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new { version, reason }) };
        return await client.SendAsync(request, Ct);
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string path, object body, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private async Task<Guid> PersonalSpaceAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accesses = await scope.ServiceProvider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(userId, Ct);
        return accesses.Single(access => access.Kind == TenantKind.Personal).TenantId;
    }

    private async Task<Guid?> ManagedByAsync(Guid userId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AsNoTracking()
            .Where(m => m.UserId == userId && m.Value == email).Select(m => m.ManagedByTenantId).SingleAsync(Ct);
    }

    private async Task AddAndVerifyEmailAsync(HttpClient client, AccountJourney account, Email email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var addedMethod = await AccountJourney.PostAsync(client, "/api/me/login-methods",
            new { email = email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, addedMethod.StatusCode);
        using var addedMethodJson = JsonDocument.Parse(await addedMethod.Content.ReadAsStringAsync(Ct));
        var methodId = addedMethodJson.RootElement.GetProperty("methodId").GetGuid();
        var code = await PickupCodeReader.ReadAsync(factory.Services, email.Value, Ct);
        using var verifiedMethod = await AccountJourney.PostAsync(client,
            $"/api/me/login-methods/{methodId:D}/verify", new { code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verifiedMethod.StatusCode);
    }

    private async Task<int> OutboxCountAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages
            .CountAsync(message => message.UserId == userId, Ct);
    }

    private async Task AgeLoginCodesAsync(string email)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
            WHERE "Destination" = @email
            """;
        command.Parameters.AddWithValue("email", email);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<Guid> CreateBusinessAsync(Guid userId)
    {
        var tenant = Tenant.CreateBusiness("Dominios " + Guid.NewGuid().ToString("N")[..8], false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private Task GrantAsync(Guid userId, IReadOnlyCollection<string> permissions)
        => PlatformOperators.GrantAsync(factory.Services, userId, permissions, Ct);

    private sealed class Dns : IDnsTxtLookup
    {
        public string? Value { get; set; }
        public Task<Result<IReadOnlyList<string>>> FindTxtRecordsAsync(string domain, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<string>>(Value is null ? [] : [Value]));
    }
}
