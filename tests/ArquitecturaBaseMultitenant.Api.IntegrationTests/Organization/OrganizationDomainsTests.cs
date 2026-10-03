using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Dns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>Verifica el TXT de dominios, la asociación persistente de correos y el retiro sobre PostgreSQL real.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class OrganizationDomainsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_verifies_removes_and_keeps_existing_login_method_associations()
    {
        var dns = new FakeDnsTxtLookup();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDnsTxtLookup>();
            services.AddSingleton<IDnsTxtLookup>(dns);
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var domain = $"corp-{Guid.NewGuid():N}.example";
        var ownerEmail = Email.Create($"owner@{domain}").Value;
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct, ownerEmail);
        var tenantId = await CreateOrganizationAsync(owner.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);

        using var registered = await AccountJourney.PostAsync(client, "/api/organization/settings/domains",
            new { domain }, Ct);
        Assert.True(registered.StatusCode == HttpStatusCode.Created,
            await registered.Content.ReadAsStringAsync(Ct));
        using var registeredJson = JsonDocument.Parse(await registered.Content.ReadAsStringAsync(Ct));
        var domainId = registeredJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(domain, registeredJson.RootElement.GetProperty("txtRecordName").GetString());
        var txtValue = registeredJson.RootElement.GetProperty("txtRecordValue").GetString()!;

        dns.SetUnavailable(true);
        using var unavailableDns = await AccountJourney.PostAsync(client,
            $"/api/organization/settings/domains/{domainId:D}/verify", new { }, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableDns.StatusCode);
        using var unavailableProblem = JsonDocument.Parse(await unavailableDns.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantDomainErrors.DnsUnavailableCode,
            unavailableProblem.RootElement.GetProperty("code").GetString());
        dns.SetUnavailable(false);

        using var missingTxt = await AccountJourney.PostAsync(client,
            $"/api/organization/settings/domains/{domainId:D}/verify", new { }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, missingTxt.StatusCode);
        using var missingTxtProblem = JsonDocument.Parse(await missingTxt.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantDomainErrors.TxtNotFoundCode, missingTxtProblem.RootElement.GetProperty("code").GetString());

        dns.Set(domain, txtValue);
        using var verified = await AccountJourney.PostAsync(client,
            $"/api/organization/settings/domains/{domainId:D}/verify", new { }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        await AssertManagedEmailAsync(owner.UserId, ownerEmail, tenantId, tenantId);

        using var colleagueClient = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var colleague = await AccountJourney.RegisterAsync(factory, colleagueClient, Ct);
        var newManagedEmail = Email.Create($"employee@{domain}").Value;
        await AddAndVerifyEmailAsync(colleagueClient, colleague, newManagedEmail);
        await AssertManagedEmailAsync(colleague.UserId, newManagedEmail, tenantId, tenantId);

        using var listed = await client.GetAsync("/api/organization/settings/domains", Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listedJson = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Ct));
        var currentDomain = Assert.Single(listedJson.RootElement.EnumerateArray());
        Assert.Equal("Verified", currentDomain.GetProperty("status").GetString());
        var version = currentDomain.GetProperty("version").GetUInt32();

        using var removed = await client.DeleteAsync(
            $"/api/organization/settings/domains/{domainId:D}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await AssertManagedEmailAsync(owner.UserId, ownerEmail, tenantId, tenantId);

        using var afterRemoval = await client.GetAsync("/api/organization/settings/domains", Ct);
        using var afterRemovalJson = JsonDocument.Parse(await afterRemoval.Content.ReadAsStringAsync(Ct));
        Assert.Empty(afterRemovalJson.RootElement.EnumerateArray());

        using var laterClient = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var laterAccount = await AccountJourney.RegisterAsync(factory, laterClient, Ct);
        var laterEmail = Email.Create($"later@{domain}").Value;
        await AddAndVerifyEmailAsync(laterClient, laterAccount, laterEmail);
        await AssertManagedEmailAsync(laterAccount.UserId, laterEmail, tenantId, expectedTenantId: null);
    }

    [Fact]
    public async Task Concurrent_organizations_cannot_register_the_same_email_domain()
    {
        using var firstClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var secondClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var firstOwner = await AccountJourney.RegisterAsync(factory, firstClient, Ct);
        var secondOwner = await AccountJourney.RegisterAsync(factory, secondClient, Ct);
        await CreateOrganizationAsync(firstOwner.UserId);
        await CreateOrganizationAsync(secondOwner.UserId);
        await AccountJourney.AuthorizeAsync(firstClient, "business", Ct);
        await AccountJourney.AuthorizeAsync(secondClient, "business", Ct);
        var domain = $"race-{Guid.NewGuid():N}.example.test";

        var responses = await Task.WhenAll(
            AccountJourney.PostAsync(firstClient, "/api/organization/settings/domains", new { domain }, Ct),
            AccountJourney.PostAsync(secondClient, "/api/organization/settings/domains", new { domain }, Ct));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Contains(new[] { firstResponse.StatusCode, secondResponse.StatusCode },
            status => status == HttpStatusCode.Created);
        var rejected = firstResponse.StatusCode == HttpStatusCode.Conflict ? firstResponse : secondResponse;
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantDomainErrors.AlreadyExistsCode, problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Organization_cannot_list_or_remove_another_organizations_email_domain()
    {
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var otherOwnerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var otherOwner = await AccountJourney.RegisterAsync(factory, otherOwnerClient, Ct);
        await CreateOrganizationAsync(owner.UserId);
        await CreateOrganizationAsync(otherOwner.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        await AccountJourney.AuthorizeAsync(otherOwnerClient, "business", Ct);
        var domain = $"private-{Guid.NewGuid():N}.example.test";

        using var created = await AccountJourney.PostAsync(ownerClient, "/api/organization/settings/domains",
            new { domain }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var domainId = createdJson.RootElement.GetProperty("id").GetGuid();

        using var ownerList = await ownerClient.GetAsync("/api/organization/settings/domains", Ct);
        Assert.Equal(HttpStatusCode.OK, ownerList.StatusCode);
        using var ownerListJson = JsonDocument.Parse(await ownerList.Content.ReadAsStringAsync(Ct));
        var ownerDomain = Assert.Single(ownerListJson.RootElement.EnumerateArray());
        var version = ownerDomain.GetProperty("version").GetUInt32();

        using var otherList = await otherOwnerClient.GetAsync("/api/organization/settings/domains", Ct);
        Assert.Equal(HttpStatusCode.OK, otherList.StatusCode);
        using var otherListJson = JsonDocument.Parse(await otherList.Content.ReadAsStringAsync(Ct));
        Assert.Empty(otherListJson.RootElement.EnumerateArray());

        using var otherRemoval = await otherOwnerClient.DeleteAsync(
            $"/api/organization/settings/domains/{domainId:D}?version={version}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, otherRemoval.StatusCode);
        using var problem = JsonDocument.Parse(await otherRemoval.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantDomainErrors.NotFoundCode, problem.RootElement.GetProperty("code").GetString());

        using var ownerListAfterAttempt = await ownerClient.GetAsync("/api/organization/settings/domains", Ct);
        using var ownerListAfterAttemptJson = JsonDocument.Parse(await ownerListAfterAttempt.Content.ReadAsStringAsync(Ct));
        Assert.Single(ownerListAfterAttemptJson.RootElement.EnumerateArray());
    }

    private async Task<Guid> CreateOrganizationAsync(Guid ownerId)
    {
        var tenant = Tenant.CreateBusiness("Dominios HTTP test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId], ownerId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant.Id;
    }

    private async Task AddAndVerifyEmailAsync(HttpClient client, AccountJourney account, Email email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var reauthTicket = await AccountJourney.IssueReauthTicketAsync(scope.ServiceProvider, account.UserId,
            Domain.Authentication.ReauthAction.AddEmail, Ct);
        using var addedMethod = await AccountJourney.PostAsync(client, "/api/me/login-methods",
            new { email = email.Value, reauthTicket }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, addedMethod.StatusCode);
        using var addedMethodJson = JsonDocument.Parse(await addedMethod.Content.ReadAsStringAsync(Ct));
        var methodId = addedMethodJson.RootElement.GetProperty("methodId").GetGuid();
        var code = await PickupCodeReader.ReadAsync(factory.Services, email.Value, Ct);
        using var verifiedMethod = await AccountJourney.PostAsync(client,
            $"/api/me/login-methods/{methodId:D}/verify", new { code }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verifiedMethod.StatusCode);
    }

    private async Task AssertManagedEmailAsync(Guid userId, Email email, Guid tenantId,
        Guid? expectedTenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var methodTenant = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var method = Assert.Single(await services.GetRequiredService<ILoginMethodRepository>()
                .ListByUserIdAsync(userId, ct), row => row.Type == Domain.Authentication.LoginMethodType.Email
                    && row.Value == email.Value);
            return Result.Success(method.ManagedByTenantId);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(expectedTenantId, methodTenant.Value);
    }

    private sealed class FakeDnsTxtLookup : IDnsTxtLookup
    {
        private readonly Dictionary<string, IReadOnlyList<string>> records = new(StringComparer.Ordinal);
        private bool unavailable;

        public void Set(string domain, string value) => records[domain] = [value];
        public void SetUnavailable(bool value) => unavailable = value;

        public Task<Result<IReadOnlyList<string>>> FindTxtRecordsAsync(string domain, CancellationToken cancellationToken) =>
            Task.FromResult(unavailable
                ? Result.Failure<IReadOnlyList<string>>(TenantDomainErrors.DnsUnavailable)
                : Result.Success(records.GetValueOrDefault(domain, [])));
    }
}
