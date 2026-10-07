using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;
using FeatureDefinition = ArquitecturaBaseMultitenant.Domain.Features.FeatureDefinition;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Host y modelo propios sobre los roles, migraciones, UoW e interceptores reales de ApiFactory.</summary>
public sealed class EngagementFactory : IAsyncLifetime
{
    private readonly ApiFactory _database;
    private readonly bool _ownsDatabase;
    private WebApplicationFactory<Program>? _host;
    internal WebApplicationFactory<Program> Host => _host ?? throw new InvalidOperationException("Initialize the fixture first.");
    internal IServiceProvider Services => Host.Services;
    internal Action<IServiceCollection>? ConfigureServices { get; init; }

    public EngagementFactory()
    {
        _database = new ApiFactory();
        _ownsDatabase = true;
    }

    internal EngagementFactory(ApiFactory database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync()
    {
        if (_ownsDatabase) await _database.InitializeAsync();
        _host = _database.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:test-deals:EnabledFor:0:Name", "Tenant");
            builder.ConfigureTestServices(services =>
            {
                // Añade el modelo al ApplicationDbContext que usa la API, conservando sus cinco interceptores.
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.ReplaceService<IModelCustomizer, IsolationModelCustomizer>());
                services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new EngagementFeatureCatalog()));
                services.RemoveAll<IFeatureFilterMetadata>();
                services.AddScoped<IFeatureFilterMetadata, EngagementTenantFeatureFilter>();
                services.AddScoped<IDealService, DealService>();
                services.AddScoped<IDealReader, DealReader>();
                services.AddScoped<IDealRepository, DealRepository>();
                services.AddScoped<IPosterService, PosterService>();
                services.AddScoped<IPosterReader, PosterReader>();
                services.AddScoped<IValidator<CreateDealRequest>, CreateDealRequestValidator>();
                services.AddScoped<IValidator<TransitionDealRequest>, TransitionDealRequestValidator>();
                services.AddScoped<IValidator<IsolationListRequest>, IsolationListRequestValidator>();
                services.AddControllers().ConfigureApplicationPartManager(parts =>
                    parts.ApplicationParts.Add(new EngagementControllerPart()));
                ConfigureServices?.Invoke(services);
            });
        });
    }

    internal HttpClient Client() => Host.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
    });

    public async ValueTask DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        if (_ownsDatabase) await _database.DisposeAsync();
    }

    private sealed class EngagementControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(EngagementControllerPart);
        public IEnumerable<TypeInfo> Types => [typeof(DealsController).GetTypeInfo(), typeof(PostersController).GetTypeInfo()];
    }
}

internal sealed class EngagementFeatureCatalog : IFeatureCatalog
{
    internal const string Deals = "test-deals";
    public IReadOnlyList<FeatureDefinition> All { get; } = [new(Deals, FeatureScope.Both, false)];
}

/// <summary>Adaptador exclusivo del módulo sintético: PublicSite elige la contraparte, nunca el tenant firmado.</summary>
[FilterAlias("Tenant")]
internal sealed class EngagementTenantFeatureFilter(ITenantContext tenant, IPublicSiteContext publicSite,
    IFeatureCatalog catalog, ITenantFeatureCache cache, TimeProvider clock) : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) => EvaluateAsync(context, CancellationToken.None);

    public async Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, CancellationToken cancellationToken)
    {
        var definition = catalog.All.SingleOrDefault(row => row.Key == context.FeatureName);
        if (definition is null || tenant.TenantKind is not { } kind || !definition.Supports(kind)) return false;
        // Decisión 2: sólo el pedido público consulta el módulo de la organización resuelta, sin Enter ni cambios
        // de claims. Lecturas y transiciones sin PublicSite conservan el gate del acceso activo.
        var tenantId = context.FeatureName == EngagementFeatureCatalog.Deals && publicSite.BusinessTenantId is { } businessId
            ? businessId : tenant.TenantId;
        if (tenantId is not { } id) return false;
        var grants = await cache.ListAsync(id, cancellationToken);
        var grant = grants.SingleOrDefault(row => row.Key == definition.Key);
        return grant?.IsEnabledAt(clock.GetUtcNow().UtcDateTime) ?? definition.DefaultEnabled;
    }
}
