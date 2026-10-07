using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Comprueba el host público, su estado real y la independencia del tenant del token.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class SubdomainTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("empresa-a.example.test:5174", true)]
    [InlineData("EMPRESA-A.EXAMPLE.TEST:5174", true)]
    [InlineData("a.example.test:5174", true)]
    [InlineData("example.test:5174", false)]
    [InlineData("a.b.example.test:5174", false)]
    [InlineData("empresa-a.other.test:5174", false)]
    [InlineData("empresa-a.example.test.evil.test:5174", false)]
    [InlineData("127.0.0.1:5174", false)]
    [InlineData("[::1]:5174", false)]
    [InlineData("empresa-a.example.test:5175", false)]
    [InlineData("empresa-a.example.test", false)]
    [InlineData("ab.example.test:5174", false)]
    [InlineData("-empresa.example.test:5174", false)]
    [InlineData("empresa_.example.test:5174", false)]
    [InlineData("émpresa.example.test:5174", false)]
    [InlineData(" empresa-a.example.test:5174", false)]
    public async Task Only_a_single_label_over_the_main_domain_is_an_organization_host(string host, bool expected)
    {
        var request = PublicRequest(host);
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));

        await middleware.InvokeAsync(request, publicSite, reader);

        Assert.Equal(expected ? 1 : 0, reader.Calls);
        Assert.Equal(expected, publicSite.BusinessTenantId is not null);
        if (expected) Assert.Equal(host.Split('.')[0].ToLowerInvariant(), publicSite.Slug);
    }

    [Fact]
    public async Task The_default_https_port_matches_the_issuer_and_an_ip_issuer_never_resolves()
    {
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test/")));
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        await middleware.InvokeAsync(PublicRequest("empresa.example.test"), publicSite, reader);
        Assert.NotNull(publicSite.BusinessTenantId);
        Assert.Equal(1, reader.Calls);

        var ipMiddleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://127.0.0.1:5174/")));
        await ipMiddleware.InvokeAsync(PublicRequest("empresa.127.0.0.1:5174"), new PublicSiteContext(), reader);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Private_routes_do_not_consult_the_public_site_reader()
    {
        var request = PublicRequest("empresa-a.example.test:5174");
        request.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "private"));
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));

        await middleware.InvokeAsync(request, publicSite, reader);

        Assert.Equal(0, reader.Calls);
        Assert.Null(publicSite.BusinessTenantId);
        Assert.Null(publicSite.Slug);
    }

    [Fact]
    public async Task The_public_site_filter_uses_the_resolved_context_without_a_second_query()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var request = PublicRequest("empresa-a.example.test:5174");
        request.RequestServices = provider;
        var reader = new RecordingReader();
        var publicSite = new PublicSiteContext();
        var middleware = new PublicSiteResolutionMiddleware(_ => Task.CompletedTask,
            new StubOrigin(new Uri("https://example.test:5174/")));
        await middleware.InvokeAsync(request, publicSite, reader);
        var filter = new PublicSiteFilter(publicSite, provider.GetRequiredService<ProblemDetailsFactory>());
        var context = new AuthorizationFilterContext(
            new ActionContext(request, new RouteData(), new ActionDescriptor()), []);

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task A_published_organization_subdomain_resolves_its_site()
    {
        var tenant = await CreateSiteAsync();
        using var client = Client();
        using var response = await client.GetAsync(SiteUrl(tenant.Slug!), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(tenant.Id, body!.RootElement.GetProperty("businessTenantId").GetGuid());
        Assert.Equal(tenant.Slug, body.RootElement.GetProperty("slug").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("privateTenantId").ValueKind);
    }

    [Fact]
    public async Task Main_domain_reserved_and_unknown_subdomains_resolve_no_site()
    {
        using var client = Client();
        await factory.Services.SeedDatabaseAsync(Ct);
        await AssertNotFoundAsync(client, "https://localhost:5174/test/access/public-site");
        await AssertNotFoundAsync(client, SiteUrl("unknown-" + Guid.NewGuid().ToString("N")));
        foreach (var reserved in ReservedTenantSlug.Defaults)
            await AssertNotFoundAsync(client, SiteUrl(reserved.Slug));

        // La reserva también bloquea un registro histórico con página publicada.
        await CreateSiteAsync(slug: "www");
        await AssertNotFoundAsync(client, SiteUrl("www"));
    }

    [Theory]
    [InlineData(TenantStatus.Active, false, false, true)]
    [InlineData(TenantStatus.Active, true, true, true)]
    [InlineData(TenantStatus.Active, true, false, false)]
    [InlineData(TenantStatus.Suspended, true, false, true)]
    [InlineData(TenantStatus.Closed, true, false, true)]
    [InlineData(TenantStatus.PendingApproval, true, false, true)]
    [InlineData(TenantStatus.Provisioning, true, false, true)]
    public async Task Unpublished_blocked_missing_or_inactive_sites_are_indistinguishable(
        TenantStatus status, bool published, bool blocked, bool withPage)
    {
        var tenant = await CreateSiteAsync(status, published, blocked, withPage);
        using var client = Client();

        await AssertNotFoundAsync(client, SiteUrl(tenant.Slug!));
    }

    [Fact]
    public async Task The_host_never_sets_the_tenant_of_a_session()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var siteA = await CreateSiteAsync();
        var siteB = await CreateSiteAsync(ownerId: account.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, siteB.Id);

        using var publicResponse = await client.GetAsync(SiteUrl(siteA.Slug!), Ct);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        using var publicBody = await publicResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteA.Id, publicBody!.RootElement.GetProperty("businessTenantId").GetGuid());
        Assert.Equal(JsonValueKind.Null, publicBody.RootElement.GetProperty("privateTenantId").ValueKind);

        using var privateResponse = await client.GetAsync(
            $"https://{siteA.Slug}.localhost:5174/test/access/private-context", Ct);
        Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        using var privateBody = await privateResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteB.Id, privateBody!.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal(JsonValueKind.Null, privateBody.RootElement.GetProperty("businessTenantId").ValueKind);
    }

    private async Task<Tenant> CreateSiteAsync(TenantStatus status = TenantStatus.Active,
        bool published = true, bool blocked = false, bool withPage = true, string? slug = null, Guid? ownerId = null)
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var tenant = Tenant.CreateBusiness("Organización pública", status == TenantStatus.PendingApproval);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug ?? "site-" + Guid.NewGuid().ToString("N")).Value).IsSuccess);
        if (status is TenantStatus.Active or TenantStatus.Suspended or TenantStatus.Closed)
            Assert.True(tenant.Activate().IsSuccess);
        if (status is TenantStatus.Suspended or TenantStatus.Closed) Assert.True(tenant.Suspend().IsSuccess);
        if (status == TenantStatus.Closed) Assert.True(tenant.Close().IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            if (ownerId is { } userId)
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            else services.GetRequiredService<ITenantRepository>().Add(tenant);
            if (withPage)
            {
                var page = PublicPage.Create();
                Assert.True(page.UpdateContent(tenant.Name, null, null, null, null, null).IsSuccess);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (published) Assert.True(page.Publish(nowUtc).IsSuccess);
                if (blocked) Assert.True(page.ApplyModerationBlock("Moderación", Guid.NewGuid(), nowUtc).IsSuccess);
                services.GetRequiredService<IPublicPageRepository>().Add(page);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return tenant;
    }

    private static async Task AssertNotFoundAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString(),
            StringComparison.OrdinalIgnoreCase);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(PublicPageErrors.NotFoundCode, body!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private static string SiteUrl(string slug) => $"https://{slug}.localhost:5174/test/access/public-site";

    private static DefaultHttpContext PublicRequest(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new PublicSiteAttribute()), "public-site"));
        return context;
    }

    private sealed class StubOrigin(Uri value) : IPublicOrigin
    {
        public Uri? Value { get; } = value;
    }

    private sealed class RecordingReader : IPublicSiteReader
    {
        public int Calls { get; private set; }

        public Task<PublicSiteRow?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<PublicSiteRow?>(new PublicSiteRow(Guid.NewGuid(), normalizedSlug));
        }
    }
}
