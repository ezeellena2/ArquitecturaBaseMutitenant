using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PublicPageModerationTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Moderation_is_authorized_audited_versioned_and_notifies_owners_without_exposing_content()
    {
        using var anonymous = Client();
        var missingPath = $"/api/platform/tenants/{Guid.NewGuid():D}/public-site";
        using var unauthenticated = await anonymous.GetAsync(missingPath, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var business = Client();
        var member = await AccountJourney.RegisterAsync(factory, business, Ct);
        var tenant = await CreateBusinessAsync(member.UserId);
        var tenantId = tenant.Id;
        var path = $"/api/platform/tenants/{tenantId:D}/public-site";
        using var consumerDenied = await business.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumerDenied.StatusCode);
        await AccountJourney.AuthorizeAsync(business, "business", Ct);
        using var saved = await business.PutAsJsonAsync("/api/organization/public-page", new
        {
            slug = tenant.Slug, name = "Página de prueba", description = "Contenido que no expone Plataforma", version = 0,
        }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using var firstPublish = await business.PostAsJsonAsync("/api/organization/public-page/publish",
            new { version = await VersionAsync(business, "/api/organization/public-page") }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, firstPublish.StatusCode);
        using var businessDenied = await business.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, businessDenied.StatusCode);

        using var platform = Client();
        var account = await AccountJourney.RegisterAsync(factory, platform, Ct);
        await GrantAsync(account.UserId, PlatformRole.Owner);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);
        var otherTenant = await CreateBusinessAsync(member.UserId);
        var otherPath = $"/api/platform/tenants/{otherTenant.Id:D}/public-site";
        Assert.Equal(0u, await VersionAsync(platform, otherPath));
        using var notFound = await platform.GetAsync(missingPath, Ct);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        using var metadata = await platform.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        using var json = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync(Ct));
        Assert.True(json.RootElement.GetProperty("isPublished").GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("description", out _));
        Assert.False(json.RootElement.TryGetProperty("logoBytes", out _));
        Assert.False(json.RootElement.TryGetProperty("contactEmail", out _));
        var version = json.RootElement.GetProperty("version").GetUInt32();
        var before = await OutboxCountAsync();
        using var invalid = await PlatformJourney.PostAsync(platform, path + "/unpublish", new { version, reason = " " }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(before, await OutboxCountAsync());

        var operation = Guid.NewGuid().ToString("D");
        using var blocked = await PostAsync(platform, path + "/unpublish", new { version, reason = "Revisar contenido" }, operation);
        Assert.Equal(HttpStatusCode.NoContent, blocked.StatusCode);
        Assert.Equal(before + 1, await OutboxCountAsync());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // El borrador privado desaparece de la lectura pública sin un alcance autorizado.
            Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .PublicPages.AnyAsync(p => p.BusinessTenantId == tenantId, Ct));
        }
        using var replay = await PostAsync(platform, path + "/unpublish", new { version, reason = "Revisar contenido" }, operation);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal(before + 1, await OutboxCountAsync());

        using var stale = await PlatformJourney.PostAsync(platform, path + "/allow-publish", new { version, reason = "Ya revisado" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(before + 1, await OutboxCountAsync());
        var blockedVersion = await VersionAsync(platform, path);
        using var organizationCannotPublish = await business.PostAsJsonAsync("/api/organization/public-page/publish", new { version = blockedVersion }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, organizationCannotPublish.StatusCode);
        using var allowed = await PlatformJourney.PostAsync(platform, path + "/allow-publish", new { version = blockedVersion, reason = "Ya revisado" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(before + 2, await OutboxCountAsync());
        var allowedVersion = await VersionAsync(platform, path);
        using var stillDraft = await platform.GetAsync(path, Ct);
        using var draftJson = JsonDocument.Parse(await stillDraft.Content.ReadAsStringAsync(Ct));
        Assert.False(draftJson.RootElement.GetProperty("isPublished").GetBoolean());
        Assert.Equal(JsonValueKind.Null, draftJson.RootElement.GetProperty("publishBlockedAtUtc").ValueKind);
        using var publish = await business.PostAsJsonAsync("/api/organization/public-page/publish", new { version = allowedVersion }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);
        Assert.Equal(0u, await VersionAsync(platform, otherPath));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var events = await db.SecurityEvents.Where(e => e.TargetTenantId == tenantId).ToArrayAsync(Ct);
            Assert.Single(events, e => e.Type.ToString() == "PublicPageUnpublished");
            Assert.Single(events, e => e.Type.ToString() == "PublicPagePublicationAllowed");
            Assert.All(events, e => Assert.Equal(account.UserId, e.ActorId));
        }
        await GrantAsync(account.UserId, PlatformRole.Support);
        using var supportDenied = await platform.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, supportDenied.StatusCode);
        using var supportCannotChange = await PlatformJourney.PostAsync(platform, path + "/unpublish", new { version = allowedVersion, reason = "Sin permiso" }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, supportCannotChange.StatusCode);
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<Tenant> CreateBusinessAsync(Guid userId)
    {
        var tenant = Tenant.CreateBusiness("Moderación " + Guid.NewGuid().ToString("N")[..8], false);
        Assert.True(tenant.Activate().IsSuccess);
        Assert.True(tenant.SetSlug(TenantSlug.Create("mod-" + Guid.NewGuid().ToString("N")[..8]).Value).IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [userId], userId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        return tenant;
    }

    private async Task GrantAsync(Guid userId, PlatformRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await manager.FindByIdAsync(userId.ToString("D"));
            Assert.NotNull(user);
            user.GrantPlatformOperator();
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
            var repository = services.GetRequiredService<IPlatformRoleAssignmentRepository>();
            var existing = await repository.GetByUserIdAsync(userId, ct);
            if (existing is null) repository.Add(PlatformRoleAssignment.Create(userId, role));
            else existing.ChangeRole(role);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<int> OutboxCountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.CountAsync(Ct);
    }

    private static async Task<uint> VersionAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string operation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", operation);
        return await client.SendAsync(request, Ct);
    }
}
