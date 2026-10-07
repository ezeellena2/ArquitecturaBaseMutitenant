using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Prepara cuenta, dos empresas y habilitaciones con la UoW real; el ingreso sigue el recorrido HTTP.</summary>
internal sealed class EngagementJourney(EngagementFactory factory, HttpClient client, AccountJourney account,
    Guid personalId, Tenant businessA, Tenant businessB) : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal const string CopiedName = "Kevin Compartido";
    internal HttpClient Client => client;
    internal AccountJourney Account => account;
    internal Guid PersonalId => personalId;
    internal Tenant BusinessA => businessA;
    internal Tenant BusinessB => businessB;
    internal static string PublicUrl(Tenant tenant, string path = "/test/deals") =>
        $"https://{tenant.Slug}.localhost:5174{path}";

    internal static async Task<EngagementJourney> CreateAsync(EngagementFactory factory)
    {
        var client = factory.Client();
        try
        {
            var account = await AccountJourney.RegisterAsync(factory.Host, client, Ct);
            using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
            var personalId = me!.RootElement.GetProperty("activeTenantId").GetGuid();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var services = scope.ServiceProvider;
                services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(personalId, TenantKind.Personal);
                var renamed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
                    services.GetRequiredService<IUserRepository>().UpdateProfileAsync(account.UserId, "Kevin", "Compartido",
                        "es-AR", "America/Argentina/Buenos_Aires", 0, ct), CommitPolicy.OnSuccess, Ct);
                Assert.True(renamed.IsSuccess);
            }
            var businessA = await CreateSiteAsync(factory, account.UserId);
            var businessB = await CreateSiteAsync(factory, account.UserId);
            var journey = new EngagementJourney(factory, client, account, personalId, businessA, businessB);
            await journey.SetModuleAsync(personalId, true);
            await journey.SetModuleAsync(businessA.Id, true);
            await journey.SetModuleAsync(businessB.Id, true);
            return journey;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    internal Task ConsumerAsync() => AccountJourney.AuthorizeAsync(client, "consumer", Ct);
    internal async Task BusinessAsync(Tenant? business = null)
    {
        var expectedId = (business ?? businessA).Id;
        await AccountJourney.AuthorizeAsync(client, "business", Ct, expectedId);
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Equal(expectedId, me!.RootElement.GetProperty("activeTenantId").GetGuid());
    }

    internal static async Task<Tenant> CreateSiteAsync(EngagementFactory factory, Guid ownerId,
        bool published = true, bool suspended = false)
    {
        var tenant = Tenant.CreateBusiness("Organización de interacción", false);
        Assert.True(tenant.SetSlug(TenantSlug.Create("eng-" + Guid.NewGuid().ToString("N")).Value).IsSuccess);
        Assert.True(tenant.Activate().IsSuccess);
        if (suspended) Assert.True(tenant.Suspend().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [ownerId], ownerId);
            var page = PublicPage.Create();
            Assert.True(page.UpdateContent(tenant.Name, null, null, null, null, null).IsSuccess);
            if (published) Assert.True(page.Publish(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
            services.GetRequiredService<IPublicPageRepository>().Add(page);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        return tenant;
    }

    internal async Task SetModuleAsync(Guid tenantId, bool enabled, DateTime? expiry = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var grant = await context.TenantFeatures.SingleOrDefaultAsync(row => row.TenantId == tenantId
                && row.FeatureKey == EngagementFeatureCatalog.Deals, ct);
            var source = expiry is null ? TenantFeatureSource.Manual : TenantFeatureSource.Trial;
            if (grant is null)
                services.GetRequiredService<ITenantFeatureRepository>().Add(
                    TenantFeature.Create(tenantId, EngagementFeatureCatalog.Deals, enabled, source, expiry).Value);
            else Assert.True(grant.Update(enabled, source, expiry).IsSuccess);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                AuditActorKind.System, Guid.Empty, tenantId, "Preparación del módulo de interacción",
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
    }

    internal async Task<IReadOnlyList<AuditEntry>> AuditAsync(Guid tenantId, Guid dealId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId,
            tenantId == personalId ? TenantKind.Personal : TenantKind.Business);
        return await services.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>().AsNoTracking()
            .Where(row => row.EntityId == dealId && row.EntityType == nameof(Deal))
            .OrderBy(row => row.OccurredAtUtc).ThenBy(row => row.Id).ToArrayAsync(Ct);
    }

    internal async Task<Poster> AddPosterAsync(Tenant business, bool published)
    {
        var poster = new Poster(Guid.Empty, published);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // Enter(id) no clasifica el acceso; Published exige Business en el interceptor real.
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(business.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            services.GetRequiredService<ApplicationDbContext>().Set<Poster>().Add(poster);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
        Assert.Equal(business.Id, poster.BusinessTenantId);
        return poster;
    }

    internal async Task UnpublishAsync(Tenant business)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(business.Id, TenantKind.Business);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var page = await services.GetRequiredService<ApplicationDbContext>().PublicPages
                .SingleAsync(item => item.BusinessTenantId == business.Id, ct);
            return page.Unpublish();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess);
    }

    public void Dispose() => client.Dispose();
}
