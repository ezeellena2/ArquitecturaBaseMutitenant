using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
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
public sealed class DirectoryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [Fact]
    public async Task Directory_lists_only_published_pages_of_active_organizations()
    {
        var prefix = await PrefixAsync();
        var listed = await CreateSiteAsync(prefix + " Publicada", withLogo: true);
        var excluded = new List<Site>
        {
            await CreateSiteAsync(prefix + " Borrador", published: false, withLogo: true),
            await CreateSiteAsync(prefix + " Bloqueada", blocked: true, withLogo: true),
            await CreateSiteAsync(prefix + " Sin página", withPage: false),
            await CreateSiteAsync(prefix + " Sin slug", withSlug: false, withLogo: true),
            await CreateSiteAsync(prefix + " Reservada", slug: "empresas", withLogo: true),
        };
        foreach (var status in new[] { TenantStatus.Suspended, TenantStatus.Closed,
            TenantStatus.PendingApproval, TenantStatus.Provisioning })
            excluded.Add(await CreateSiteAsync(prefix + " " + status, status, withLogo: true));

        using var client = Client();
        var page = await ReadPageAsync(client, prefix);
        var row = Assert.Single(page.Items);
        Assert.Equal(listed.Tenant.Slug, row.Slug);
        Assert.Equal(1, page.TotalCount);
        foreach (var hidden in excluded)
        {
            Assert.DoesNotContain(page.Items, entry => entry.Name == hidden.Tenant.Name);
            if (hidden.Tenant.Slug is { } slug) await AssertLogoNotFoundAsync(client, slug);
        }
    }

    [Fact]
    public async Task Directory_search_ignores_accents_and_case_and_escapes_wildcards()
    {
        var prefix = await PrefixAsync();
        var perez = await CreateSiteAsync(prefix + " Pérez", description: prefix + " descripción exclusiva");
        var percent = await CreateSiteAsync(prefix + " 50%");
        await CreateSiteAsync(prefix + " 50x");
        var underscore = await CreateSiteAsync(prefix + " A_B");
        await CreateSiteAsync(prefix + " A-B");
        using var client = Client();

        Assert.Equal(perez.Tenant.Slug, Assert.Single((await ReadPageAsync(client, prefix + " pEREZ")).Items).Slug);
        Assert.Equal(percent.Tenant.Slug, Assert.Single((await ReadPageAsync(client, prefix + " 50%")).Items).Slug);
        Assert.Equal(underscore.Tenant.Slug, Assert.Single((await ReadPageAsync(client, prefix + " A_")).Items).Slug);
        Assert.Empty((await ReadPageAsync(client, prefix + " descripcion exclusiva")).Items);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task Directory_pages_by_ten_and_an_out_of_range_page_is_empty(int count)
    {
        var prefix = await PrefixAsync();
        for (var index = 0; index < count; index++)
            await CreateSiteAsync(prefix + " Empresa " + index.ToString("D2", CultureInfo.InvariantCulture));
        using var client = Client();

        // ApiTestGroup conserva páginas de otras clases: Search aísla este conjunto, sin fijar pageSize.
        var first = await ReadPageAsync(client, prefix);
        Assert.Equal(1, first.Page);
        Assert.Equal(10, first.PageSize);
        Assert.Equal(Math.Min(10, count), first.Items.Count);
        Assert.Equal(count, first.TotalCount);
        Assert.Equal(count == 50 ? 5 : 1, first.TotalPages);

        foreach (var size in new[] { 20, 50, 100 })
        {
            var allowed = await ReadPageAsync(client, prefix, "pageSize=" + size.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(size, allowed.PageSize);
            Assert.Equal(Math.Min(size, count), allowed.Items.Count);
            Assert.Equal(count, allowed.TotalCount);
        }

        var outside = await ReadPageAsync(client, prefix, "page=8");
        Assert.Equal(8, outside.Page);
        Assert.Empty(outside.Items);
        Assert.Equal(count, outside.TotalCount);
        Assert.Equal(first.TotalPages, outside.TotalPages);
        if (count == 50)
        {
            var last = await ReadPageAsync(client, prefix, "page=5");
            Assert.Equal(10, last.Items.Count);
            Assert.False(last.HasNext);
            Assert.Empty(first.Items.Select(row => row.Slug).Intersect(last.Items.Select(row => row.Slug)));
        }
    }

    [Fact]
    public async Task Directory_orders_by_name_using_spanish_collation_and_an_id_tie_breaker()
    {
        var prefix = await PrefixAsync();
        foreach (var name in new[] { "Zapata", "Ñandú", "Nuñez", "Álvarez" })
            await CreateSiteAsync(prefix + " " + name);
        var repeated = await CreateSiteAsync(prefix + " Nuñez");
        var repeatedAgain = await CreateSiteAsync(prefix + " Nuñez");
        using var client = Client();

        var ascending = await ReadPageAsync(client, prefix);
        Assert.Equal(new[] { "Álvarez", "Nuñez", "Nuñez", "Nuñez", "Ñandú", "Zapata" },
            ascending.Items.Select(row => row.Name[(prefix.Length + 1)..]));
        var ties = ascending.Items.Where(row => row.Slug == repeated.Tenant.Slug || row.Slug == repeatedAgain.Tenant.Slug);
        Assert.Equal(new[] { repeated, repeatedAgain }.OrderBy(site => site.PageId).Select(site => site.Tenant.Slug),
            ties.Select(row => row.Slug));

        var descending = await ReadPageAsync(client, prefix, "sort=-name");
        Assert.Equal(new[] { "Zapata", "Ñandú", "Nuñez", "Nuñez", "Nuñez", "Álvarez" },
            descending.Items.Select(row => row.Name[(prefix.Length + 1)..]));
    }

    [Theory]
    [InlineData("sort=slug", "sort")]
    [InlineData("sort=createdAtUtc", "sort")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=11", "pageSize")]
    public async Task Directory_rejects_invalid_pagination_and_unknown_sorts(string query, string field)
    {
        using var client = Client();
        using var response = await client.GetAsync("/api/directory?" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Validation.Failed", problem!.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Directory_rows_carry_only_public_fields()
    {
        var prefix = await PrefixAsync();
        var withLogo = await CreateSiteAsync(prefix + " A", withLogo: true);
        await CreateSiteAsync(prefix + " B", description: null);
        using var client = Client();
        using var response = await client.GetAsync(DirectoryUrl(prefix), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var rows = json!.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        foreach (var row in rows)
            Assert.Equal(new[] { "description", "logoUrl", "name", "slug" },
                row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(LogoUrl(withLogo), rows[0].GetProperty("logoUrl").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("logoUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task Directory_logo_is_served_only_for_listed_organizations_and_revalidates_cached_images()
    {
        var prefix = await PrefixAsync();
        var site = await CreateSiteAsync(prefix + " Logo", withLogo: true);
        var noLogo = await CreateSiteAsync(prefix + " Sin logo");
        var suspended = await CreateSiteAsync(prefix + " Suspendida después", withLogo: true);
        using var client = Client();
        using var image = await client.GetAsync(LogoUrl(site), Ct);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync(Ct));
        Assert.True(image.Headers.CacheControl?.MustRevalidate);
        Assert.True(image.Headers.CacheControl?.Public);
        Assert.NotNull(image.Headers.ETag);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, LogoUrl(site));
        conditional.Headers.IfNoneMatch.Add(image.Headers.ETag!);
        using var unchanged = await client.SendAsync(conditional, Ct);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Equal(image.Headers.ETag, unchanged.Headers.ETag);
        Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync(Ct));

        await AssertLogoNotFoundAsync(client, noLogo.Tenant.Slug!);
        await AssertLogoNotFoundAsync(client, "unknown-" + Guid.NewGuid().ToString("N"));
        await ChangePageAsync(site, page => page.Unpublish());
        using var cached = new HttpRequestMessage(HttpMethod.Get, LogoUrl(site));
        cached.Headers.IfNoneMatch.Add(image.Headers.ETag!);
        using var withdrawn = await client.SendAsync(cached, Ct);
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);

        using var activeImage = await client.GetAsync(LogoUrl(suspended), Ct);
        Assert.Equal(HttpStatusCode.OK, activeImage.StatusCode);
        await SuspendAsync(suspended.Tenant);
        using var cachedSuspended = new HttpRequestMessage(HttpMethod.Get, LogoUrl(suspended));
        cachedSuspended.Headers.IfNoneMatch.Add(activeImage.Headers.ETag!);
        using var hidden = await client.SendAsync(cachedSuspended, Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Own_draft_is_hidden_even_with_a_business_session_and_the_public_filter()
    {
        var prefix = await PrefixAsync();
        using var client = Client();
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var draft = await CreateSiteAsync(prefix + " Borrador propio", published: false, withLogo: true, ownerId: owner.UserId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, draft.Tenant.Id);
        Assert.Empty((await ReadPageAsync(client, prefix)).Items);
        await AssertLogoNotFoundAsync(client, draft.Tenant.Slug!);

        // Public permite el borrador al dueño; el reader debe mantener las condiciones de publicación explícitas.
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(draft.Tenant.Id, TenantKind.Business);
        var reader = scope.ServiceProvider.GetRequiredService<IDirectoryReader>();
        Assert.Empty((await reader.ListAsync(new ListDirectoryRequest { Search = prefix }, Ct)).Items);
        Assert.Null(await reader.FindLogoAsync(draft.Tenant.Slug!, Ct));
    }

    private async Task<string> PrefixAsync()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        return "Directorio " + Guid.NewGuid().ToString("N");
    }

    private async Task<Site> CreateSiteAsync(string name, TenantStatus status = TenantStatus.Active,
        bool published = true, bool blocked = false, bool withPage = true, bool withSlug = true,
        bool withLogo = false, string? slug = null, Guid? ownerId = null, string? description = "Descripción pública")
    {
        var tenant = Tenant.CreateBusiness(name, status == TenantStatus.PendingApproval);
        if (withSlug)
            Assert.True(tenant.SetSlug(TenantSlug.Create(slug ?? "directory-" + Guid.NewGuid().ToString("N")).Value).IsSuccess);
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
                Assert.True(page.UpdateContent(name, description, Email.Create("contacto@example.test").Value,
                    PhoneNumber.Create("+541145678900").Value, "Buenos Aires", "https://example.test").IsSuccess);
                if (withLogo) Assert.True(page.SetLogo(Png, "image/png").IsSuccess);
                var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                if (published) Assert.True(page.Publish(nowUtc).IsSuccess);
                if (blocked) Assert.True(page.ApplyModerationBlock("Moderación", Guid.NewGuid(), nowUtc).IsSuccess);
                services.GetRequiredService<IPublicPageRepository>().Add(page);
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return new Site(tenant, page?.Id ?? Guid.Empty, page?.Version ?? 0);
    }

    private async Task ChangePageAsync(Site site, Func<PublicPage, Result> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(site.Tenant.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = services.GetRequiredService<IPublicPageRepository>();
            var page = await repository.GetCurrentAsync(site.Version, ct);
            Assert.NotNull(page);
            var changed = change(page);
            if (changed.IsFailure) return changed;
            repository.MarkUpdated(page);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    private async Task SuspendAsync(Tenant tenant)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenant.Id, ct);
            Assert.NotNull(tracked);
            return tracked.Suspend();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    private static async Task<PagedResult<DirectoryEntryRow>> ReadPageAsync(HttpClient client, string search, string query = "")
    {
        using var response = await client.GetAsync(DirectoryUrl(search, query), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<PagedResult<DirectoryEntryRow>>(await response.Content.ReadFromJsonAsync<PagedResult<DirectoryEntryRow>>(Ct));
    }

    private static async Task AssertLogoNotFoundAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync("/api/directory/" + slug + "/logo", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(PublicPageErrors.NotFoundCode, problem!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private static string DirectoryUrl(string search, string query = "") =>
        "/api/directory?search=" + Uri.EscapeDataString(search) + (query.Length == 0 ? "" : "&" + query);

    private static string LogoUrl(Site site) => "/api/directory/" + site.Tenant.Slug + "/logo?v="
        + site.Version.ToString(CultureInfo.InvariantCulture);

    private sealed record Site(Tenant Tenant, Guid PageId, uint Version);
}
