using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.PublicSite;

[Collection(ApiTestGroup.Name)]
public sealed class PublicSiteTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Published_page_shows_only_its_public_fields()
    {
        var site = await CreateSiteAsync(logoContentType: "image/png");
        using var client = Client();
        using var response = await client.GetAsync(SiteUrl(site.Tenant), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var page = json!.RootElement;
        Assert.Equal(new[] { "address", "description", "email", "logoUrl", "name", "phone", "slug", "website" },
            page.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(site.Tenant.Slug, page.GetProperty("slug").GetString());
        Assert.Equal(site.Tenant.Name, page.GetProperty("name").GetString());
        Assert.Equal("Una organización y sus empresas.", page.GetProperty("description").GetString());
        Assert.Equal("Buenos Aires", page.GetProperty("address").GetString());
        Assert.Equal("+541145678900", page.GetProperty("phone").GetString());
        Assert.Equal("contacto@example.test", page.GetProperty("email").GetString());
        Assert.Equal("https://example.test", page.GetProperty("website").GetString());
        Assert.Equal(LogoUrl(site.Version), page.GetProperty("logoUrl").GetString());
    }

    [Fact]
    public async Task Optional_fields_are_null_and_a_missing_logo_answers_not_found()
    {
        var site = await CreateSiteAsync(withContact: false);
        using var client = Client();
        using var response = await client.GetAsync(SiteUrl(site.Tenant), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        foreach (var property in new[] { "description", "address", "phone", "email", "website", "logoUrl" })
            Assert.Equal(JsonValueKind.Null, json!.RootElement.GetProperty(property).ValueKind);

        await ReadNotFoundAsync(client, SiteUrl(site.Tenant, "/logo"));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public async Task Logo_is_served_with_its_type_and_answers_304_when_unchanged(string contentType)
    {
        var site = await CreateSiteAsync(logoContentType: contentType);
        using var client = Client();
        var url = SiteUrl(site.Tenant, "/logo?v=" + site.Version.ToString(CultureInfo.InvariantCulture));
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(LogoBytes(contentType), await response.Content.ReadAsByteArrayAsync(Ct));
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);

        // Las formas débiles y las listas de If-None-Match también corresponden al mismo archivo.
        foreach (var condition in new[]
        {
            etag.ToString(),
            "\"otro-logo\", W/" + etag,
            "*",
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("If-None-Match", condition);
            using var unchanged = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
            Assert.Equal(etag, unchanged.Headers.ETag);
            Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync(Ct));
        }
    }

    [Fact]
    public async Task Replacing_the_logo_changes_its_url_and_etag_and_unpublishing_hides_cached_logos()
    {
        var site = await CreateSiteAsync(logoContentType: "image/png");
        using var client = Client();
        using var oldLogo = await client.GetAsync(SiteUrl(site.Tenant, "/logo"), Ct);
        Assert.Equal(HttpStatusCode.OK, oldLogo.StatusCode);
        var newVersion = await ChangePageAsync(site.Tenant, site.Version, page => page.SetLogo(LogoBytes("image/jpeg"), "image/jpeg"));
        Assert.NotEqual(site.Version, newVersion);

        using var pageResponse = await client.GetAsync(SiteUrl(site.Tenant), Ct);
        using var json = await pageResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(LogoUrl(newVersion), json!.RootElement.GetProperty("logoUrl").GetString());
        using var request = new HttpRequestMessage(HttpMethod.Get, SiteUrl(site.Tenant, "/logo"));
        request.Headers.IfNoneMatch.Add(oldLogo.Headers.ETag!);
        using var newLogo = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, newLogo.StatusCode);
        Assert.NotEqual(oldLogo.Headers.ETag, newLogo.Headers.ETag);
        Assert.Equal("image/jpeg", newLogo.Content.Headers.ContentType?.MediaType);

        await ChangePageAsync(site.Tenant, newVersion, page => page.Unpublish());
        using var cachedRequest = new HttpRequestMessage(HttpMethod.Get, SiteUrl(site.Tenant, "/logo"));
        cachedRequest.Headers.IfNoneMatch.Add(newLogo.Headers.ETag!);
        using var hidden = await client.SendAsync(cachedRequest, Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Not_available_answers_the_same_for_every_reason()
    {
        using var client = Client();
        await factory.Services.SeedDatabaseAsync(Ct);
        var hosts = new List<string> { "localhost", "www.localhost", "unknown-" + Guid.NewGuid().ToString("N") + ".localhost" };
        var states = new[]
        {
            (TenantStatus.Active, false, false, true),
            (TenantStatus.Active, true, true, true),
            (TenantStatus.Active, true, false, false),
            (TenantStatus.Suspended, true, false, true),
            (TenantStatus.Closed, true, false, true),
            (TenantStatus.PendingApproval, true, false, true),
            (TenantStatus.Provisioning, true, false, true),
        };
        foreach (var (status, published, blocked, withPage) in states)
        {
            var site = await CreateSiteAsync(status, published, blocked, withPage, logoContentType: "image/png");
            hosts.Add(site.Tenant.Slug + ".localhost");

            // Incluso el dueño que puede leer el borrador con el filtro Public no lo expone desde el reader público.
            await using var scope = factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(site.Tenant.Id, TenantKind.Business);
            var reader = services.GetRequiredService<IPublicPageReader>();
            Assert.Null(await reader.FindPublishedAsync(site.Tenant.Id, site.Tenant.Slug!, Ct));
            Assert.Null(await reader.FindPublishedLogoAsync(site.Tenant.Id, site.Tenant.Slug!, Ct));
        }

        JsonObject? expected = null;
        foreach (var host in hosts)
        foreach (var suffix in new[] { "", "/logo" })
        {
            var problem = await ReadNotFoundAsync(client, $"https://{host}:5174/api/public-site{suffix}");
            expected ??= problem;
            Assert.True(JsonNode.DeepEquals(expected, problem), "No disponible debe tener el mismo cuerpo salvo traceId.");
        }
    }

    [Fact]
    public async Task Draft_page_is_not_available_even_for_its_own_members()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var site = await CreateSiteAsync(published: false, logoContentType: "image/png", ownerId: account.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, site.Tenant.Id);

        using var ownPage = await client.GetAsync("/api/organization/public-page", Ct);
        Assert.Equal(HttpStatusCode.OK, ownPage.StatusCode);
        using var json = await ownPage.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.False(json!.RootElement.GetProperty("isPublished").GetBoolean());
        await ReadNotFoundAsync(client, SiteUrl(site.Tenant));
        await ReadNotFoundAsync(client, SiteUrl(site.Tenant, "/logo"));
    }

    [Fact]
    public async Task Subdomain_resolves_public_data_only_and_keeps_the_private_tenant_from_the_token()
    {
        using var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var siteA = await CreateSiteAsync(name: "Publicación de A", logoContentType: "image/png");
        var siteB = await CreateSiteAsync(name: "Borrador privado de B", published: false, ownerId: account.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, siteB.Tenant.Id);

        using var publicResponse = await client.GetAsync(SiteUrl(siteA.Tenant), Ct);
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        using var publicJson = await publicResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteA.Tenant.Name, publicJson!.RootElement.GetProperty("name").GetString());
        Assert.Equal(siteA.Tenant.Slug, publicJson.RootElement.GetProperty("slug").GetString());

        using var privateResponse = await client.GetAsync($"https://{siteA.Tenant.Slug}.localhost:5174/api/organization/public-page", Ct);
        Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        using var privateJson = await privateResponse.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(siteB.Tenant.Name, privateJson!.RootElement.GetProperty("name").GetString());
        Assert.Equal(siteB.Tenant.Slug, privateJson.RootElement.GetProperty("slug").GetString());
        Assert.False(privateJson.RootElement.GetProperty("isPublished").GetBoolean());
    }

    [Fact]
    public async Task Public_readers_require_the_resolved_tenant_and_slug_to_match()
    {
        var siteA = await CreateSiteAsync(logoContentType: "image/png");
        var siteB = await CreateSiteAsync(logoContentType: "image/jpeg");
        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPublicPageReader>();
        Assert.NotNull(await reader.FindPublishedAsync(siteA.Tenant.Id, siteA.Tenant.Slug!, Ct));
        Assert.NotNull(await reader.FindPublishedLogoAsync(siteA.Tenant.Id, siteA.Tenant.Slug!, Ct));
        Assert.Null(await reader.FindPublishedAsync(siteA.Tenant.Id, siteB.Tenant.Slug!, Ct));
        Assert.Null(await reader.FindPublishedLogoAsync(siteA.Tenant.Id, siteB.Tenant.Slug!, Ct));
    }

    private async Task<Site> CreateSiteAsync(TenantStatus status = TenantStatus.Active,
        bool published = true, bool blocked = false, bool withPage = true, bool withContact = true,
        string? logoContentType = null, Guid? ownerId = null, string name = "Grupo público")
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var tenant = Tenant.CreateBusiness(name, status == TenantStatus.PendingApproval);
        Assert.True(tenant.SetSlug(TenantSlug.Create("page-" + Guid.NewGuid().ToString("N")).Value).IsSuccess);
        if (status is TenantStatus.Active or TenantStatus.Suspended or TenantStatus.Closed)
            Assert.True(tenant.Activate().IsSuccess);
        if (status is TenantStatus.Suspended or TenantStatus.Closed) Assert.True(tenant.Suspend().IsSuccess);
        if (status == TenantStatus.Closed) Assert.True(tenant.Close().IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        PublicPage? page = null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            if (ownerId is { } userId)
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            else services.GetRequiredService<ITenantRepository>().Add(tenant);
            if (withPage)
            {
                page = PublicPage.Create();
                Assert.True(page.UpdateContent(tenant.Name, withContact ? "Una organización y sus empresas." : null,
                    withContact ? Email.Create("contacto@example.test").Value : null,
                    withContact ? PhoneNumber.Create("+541145678900").Value : null,
                    withContact ? "Buenos Aires" : null, withContact ? "https://example.test" : null).IsSuccess);
                if (logoContentType is not null) Assert.True(page.SetLogo(LogoBytes(logoContentType), logoContentType).IsSuccess);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (published) Assert.True(page.Publish(nowUtc).IsSuccess);
                if (blocked) Assert.True(page.ApplyModerationBlock("Moderación", Guid.NewGuid(), nowUtc).IsSuccess);
                services.GetRequiredService<IPublicPageRepository>().Add(page);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return new Site(tenant, page?.Version ?? 0);
    }

    private async Task<uint> ChangePageAsync(Tenant tenant, uint version, Func<PublicPage, Result> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        PublicPage? page = null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = services.GetRequiredService<IPublicPageRepository>();
            page = await repository.GetCurrentAsync(version, ct);
            Assert.NotNull(page);
            var changed = change(page);
            if (changed.IsFailure) return changed;
            repository.MarkUpdated(page);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return page!.Version;
    }

    private static async Task<JsonObject> ReadNotFoundAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!.AsObject();
        Assert.Equal(PublicPageErrors.NotFoundCode, problem["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(problem["traceId"]!.GetValue<string>()));
        problem.Remove("traceId");
        return problem;
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private static string SiteUrl(Tenant tenant, string suffix = "") => $"https://{tenant.Slug}.localhost:5174/api/public-site{suffix}";
    private static string LogoUrl(uint version) => "/api/public-site/logo?v=" + version.ToString(CultureInfo.InvariantCulture);
    private static byte[] LogoBytes(string contentType) => contentType switch
    {
        "image/png" => [137, 80, 78, 71, 13, 10, 26, 10, 1],
        "image/jpeg" => [0xff, 0xd8, 0xff, 1],
        "image/webp" => [82, 73, 70, 70, 4, 0, 0, 0, 87, 69, 66, 80],
        _ => throw new ArgumentOutOfRangeException(nameof(contentType)),
    };

    private sealed record Site(Tenant Tenant, uint Version);
}
