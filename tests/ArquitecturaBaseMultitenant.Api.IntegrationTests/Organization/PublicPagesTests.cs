using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

[Collection(ApiTestGroup.Name)]
public sealed class PublicPagesTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_edits_publishes_and_cannot_republish_a_moderated_page_or_read_another_tenant_page()
    {
        using var ownerClient = Client();
        using var otherClient = Client();
        var owner = await AccountJourney.RegisterAsync(factory, ownerClient, Ct);
        var other = await AccountJourney.RegisterAsync(factory, otherClient, Ct);
        var tenant = await CreateBusinessAsync("Página pública test", "pagina-test", owner.UserId);
        var otherTenant = await CreateBusinessAsync("Otra página test", "otra-pagina", other.UserId);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", Ct);
        await AccountJourney.AuthorizeAsync(otherClient, "business", Ct);

        using var initial = await ownerClient.GetAsync("/api/organization/public-page", Ct);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using var initialJson = JsonDocument.Parse(await initial.Content.ReadAsStringAsync(Ct));
        Assert.Equal("pagina-test", initialJson.RootElement.GetProperty("slug").GetString());
        Assert.False(initialJson.RootElement.GetProperty("isPublished").GetBoolean());
        Assert.Equal(0u, initialJson.RootElement.GetProperty("version").GetUInt32());

        var png = Convert.ToBase64String([137, 80, 78, 71, 13, 10, 26, 10, 1]);
        using var updated = await ownerClient.PutAsJsonAsync("/api/organization/public-page", new
        {
            slug = "pagina-test-2",
            name = "Grupo Delta",
            description = "Una organización y sus empresas.",
            contactEmail = "contacto@example.com",
            contactPhone = "+541145678900",
            address = "Buenos Aires",
            website = "https://example.com",
            logoBase64 = png,
            logoContentType = "image/png",
            removeLogo = false,
            version = 0,
        }, Ct);
        Assert.True(updated.StatusCode == HttpStatusCode.NoContent,
            await updated.Content.ReadAsStringAsync(Ct));

        using var saved = await ownerClient.GetAsync("/api/organization/public-page", Ct);
        using var savedJson = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Ct));
        var page = savedJson.RootElement;
        Assert.Equal("pagina-test-2", page.GetProperty("slug").GetString());
        Assert.Equal("Grupo Delta", page.GetProperty("name").GetString());
        Assert.StartsWith("data:image/png;base64,", page.GetProperty("logoDataUri").GetString());
        var savedVersion = page.GetProperty("version").GetUInt32();

        using var stale = await ownerClient.PutAsJsonAsync("/api/organization/public-page", new
        {
            slug = "pagina-test-2",
            name = "Edición obsoleta",
            description = (string?)null,
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            address = (string?)null,
            website = (string?)null,
            logoBase64 = (string?)null,
            logoContentType = (string?)null,
            removeLogo = false,
            version = 0,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var publish = await ownerClient.PostAsJsonAsync("/api/organization/public-page/publish",
            new { version = savedVersion }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);
        using var published = await ownerClient.GetAsync("/api/organization/public-page", Ct);
        using var publishedJson = JsonDocument.Parse(await published.Content.ReadAsStringAsync(Ct));
        Assert.True(publishedJson.RootElement.GetProperty("isPublished").GetBoolean());
        var publishedVersion = publishedJson.RootElement.GetProperty("version").GetUInt32();

        await BlockPageAsync(tenant.Id, owner.UserId, publishedVersion);
        using var moderated = await ownerClient.GetAsync("/api/organization/public-page", Ct);
        using var moderatedJson = JsonDocument.Parse(await moderated.Content.ReadAsStringAsync(Ct));
        var blockedVersion = moderatedJson.RootElement.GetProperty("version").GetUInt32();
        Assert.False(moderatedJson.RootElement.GetProperty("isPublished").GetBoolean());
        Assert.Equal("Datos de contacto falsos.", moderatedJson.RootElement.GetProperty("publishBlockedReason").GetString());

        using var republish = await ownerClient.PostAsJsonAsync("/api/organization/public-page/publish",
            new { version = blockedVersion }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, republish.StatusCode);
        using var blockedProblem = JsonDocument.Parse(await republish.Content.ReadAsStringAsync(Ct));
        Assert.Equal(PublicPageErrors.PublishBlockedCode, blockedProblem.RootElement.GetProperty("code").GetString());

        using var otherPage = await otherClient.GetAsync("/api/organization/public-page", Ct);
        Assert.Equal(HttpStatusCode.OK, otherPage.StatusCode);
        using var otherPageJson = JsonDocument.Parse(await otherPage.Content.ReadAsStringAsync(Ct));
        Assert.Equal(otherTenant.Name, otherPageJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("otra-pagina", otherPageJson.RootElement.GetProperty("slug").GetString());

        using var taken = await ownerClient.PutAsJsonAsync("/api/organization/public-page", new
        {
            slug = "otra-pagina",
            name = "Grupo Delta",
            version = blockedVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        using var takenProblem = JsonDocument.Parse(await taken.Content.ReadAsStringAsync(Ct));
        Assert.Equal(TenantSlugErrors.TakenCode, takenProblem.RootElement.GetProperty("code").GetString());

        using var reserved = await ownerClient.PutAsJsonAsync("/api/organization/public-page", new
        {
            slug = "admin",
            name = "Grupo Delta",
            version = blockedVersion,
        }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, reserved.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private async Task<Tenant> CreateBusinessAsync(string name, string slug, Guid userId)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create(slug).Value).IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task BlockPageAsync(Guid tenantId, Guid actorId, uint version)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var repository = services.GetRequiredService<IPublicPageRepository>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var page = await repository.GetCurrentAsync(version, ct);
            Assert.NotNull(page);
            var blocked = page.ApplyModerationBlock("Datos de contacto falsos.", actorId,
                new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
            if (blocked.IsFailure) return blocked;
            repository.MarkUpdated(page);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }
}
