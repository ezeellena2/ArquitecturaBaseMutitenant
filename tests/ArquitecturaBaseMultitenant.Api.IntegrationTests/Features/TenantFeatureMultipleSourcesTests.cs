using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

/// <summary>
/// Una fila por origen (ADR 0069), cargadas directo en la base porque nadie escribe todavía Approved ni Subscription: el
/// módulo está prendido si alguna fila de esa organización está vigente, en <c>[FeatureGate]</c>, en <c>/api/me</c> y en
/// la política de WhatsApp sin sesión; la pestaña de Plataforma y su <c>PUT</c> ven y editan solo la fila de Plataforma.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class TenantFeatureMultipleSourcesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Any_current_row_of_the_organization_turns_the_module_on()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await isolated.Services.SeedDatabaseAsync(Ct);
        var clock = new FakeTimeProvider(isolated.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        using var host = isolated.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FeatureManagement:test-business:EnabledFor:0:Name", "Tenant");
            builder.UseSetting("FeatureManagement:test-consumer:EnabledFor:0:Name", "Tenant");
            builder.UseSetting("FeatureManagement:test-emergency:EnabledFor:0:Name", "Tenant");
            builder.UseSetting("FeatureManagement:test-emergency", "false");
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new TestFeatureCatalog()));
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            });
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await SampleAccountsFixture.SeedAsync(host.Services, account.Email.Value, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var initialMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var businessId = initialMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        var otherId = await CreateBusinessAsync(host.Services, "Otra organización de módulos");
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // La fila vigente de otra organización no prende nada en esta.
        await AddAsync(host.Services, otherId, "test-business", true, TenantFeatureSource.Subscription, null);
        await AddAsync(host.Services, otherId, "test-business", true, TenantFeatureSource.Approved, null);
        await AssertBusinessModuleAsync(client, false);

        // Con filas, ninguna vigente: apagado. Una aprobada vigente al lado de una manual apagada lo prende.
        await AddAsync(host.Services, businessId, "test-business", false, TenantFeatureSource.Manual, null);
        await AssertBusinessModuleAsync(client, false);
        await AddAsync(host.Services, businessId, "test-business", true, TenantFeatureSource.Approved, nowUtc.AddMinutes(1));
        await AssertBusinessModuleAsync(client, true);

        // La aprobada vence en su instante aunque las filas sigan en la caché; una suscripción vigente lo vuelve a prender.
        clock.Advance(TimeSpan.FromMinutes(1));
        await AssertBusinessModuleAsync(client, false);
        await AddAsync(host.Services, businessId, "test-business", true, TenantFeatureSource.Subscription, null);
        await AssertBusinessModuleAsync(client, true);

        // El apagado de emergencia sigue mandando sobre cualquier fila vigente.
        await AddAsync(host.Services, businessId, "test-emergency", true, TenantFeatureSource.Subscription, null);
        using var emergency = await client.GetAsync("/test/features/emergency", Ct);
        Assert.Equal(HttpStatusCode.NotFound, emergency.StatusCode);

        // Consumer no ve un módulo Business aunque su espacio tenga una fila vigente.
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using var consumerMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var personalId = consumerMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        await AddAsync(host.Services, personalId, "test-business", true, TenantFeatureSource.Subscription, null);
        using var personalMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.DoesNotContain(personalMe!.RootElement.GetProperty("features").EnumerateArray(), row => row.GetString() == "test-business");
        using var wrongAccess = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongAccess.StatusCode);
    }

    [Fact]
    public async Task WhatsApp_policy_without_a_session_reads_every_row_of_its_organization_only()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        await isolated.Services.SeedDatabaseAsync(Ct);
        var clock = new FakeTimeProvider(isolated.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
        var organizationId = await CreateBusinessAsync(host.Services, "Organización con WhatsApp");
        var otherId = await CreateBusinessAsync(host.Services, "Otra organización con WhatsApp");
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        Assert.False(await IsWhatsAppEnabledAsync(host.Services, organizationId));
        await AddAsync(host.Services, otherId, WhatsAppFeature.Key, true, TenantFeatureSource.Subscription, null);
        Assert.False(await IsWhatsAppEnabledAsync(host.Services, organizationId));
        Assert.True(await IsWhatsAppEnabledAsync(host.Services, otherId));

        await AddAsync(host.Services, organizationId, WhatsAppFeature.Key, false, TenantFeatureSource.Manual, null);
        await AddAsync(host.Services, organizationId, WhatsAppFeature.Key, true, TenantFeatureSource.Trial, nowUtc.AddMinutes(1));
        Assert.True(await IsWhatsAppEnabledAsync(host.Services, organizationId));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(await IsWhatsAppEnabledAsync(host.Services, organizationId));
        await AddAsync(host.Services, organizationId, WhatsAppFeature.Key, true, TenantFeatureSource.Approved, null);
        Assert.True(await IsWhatsAppEnabledAsync(host.Services, organizationId));
    }

    [Fact]
    public async Task Platform_tab_and_its_put_read_and_edit_only_the_platform_row()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IFeatureCatalog>(new TestFeatureCatalog()))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await SampleAccountsFixture.SeedAsync(host.Services, account.Email.Value, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var businessMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var tenantId = businessMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var path = $"/api/platform/tenants/{tenantId:D}/features";
        await AddAsync(host.Services, tenantId, "test-business", true, TenantFeatureSource.Subscription, null);
        var subscription = await ReadRowAsync(host.Services, tenantId, TenantFeatureSource.Subscription);

        // La pestaña no muestra la suscripción: sin fila de Plataforma, el valor de entrada con versión 0.
        var initial = await ReadTabRowAsync(client, path);
        Assert.Equal((false, "Plan", 0u), (initial.Enabled, initial.Source, initial.Version));

        // El PUT crea la fila de Plataforma al lado y no toca la suscripción.
        using var saved = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = false, source = "Manual", version = 0u, reason = "Apagado manual" }, Ct);
        var manualVersion = await SavedVersion.ReadAsync(saved, Ct);
        Assert.Equal((false, "Manual", manualVersion), await ReadTabRowAsync(client, path));
        Assert.Equal(subscription, await ReadRowAsync(host.Services, tenantId, TenantFeatureSource.Subscription));

        // Con dos filas de Plataforma, que solo arma un test, la pestaña y el PUT toman la más reciente.
        var nowUtc = host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var expiresOn = DateOnly.FromDateTime(nowUtc).AddDays(2);
        await AddAsync(host.Services, tenantId, "test-business", true, TenantFeatureSource.Trial, nowUtc.AddDays(1));
        var trial = await ReadRowAsync(host.Services, tenantId, TenantFeatureSource.Trial);
        Assert.Equal((true, "Trial", trial.Version), await ReadTabRowAsync(client, path));
        using var stale = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = true, source = "Trial", expiresOn, version = manualVersion, reason = "Versión de la fila vieja" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var extended = await client.PutAsJsonAsync(path + "/test-business", new
        { enabled = true, source = "Trial", expiresOn, version = trial.Version, reason = "Prueba extendida" }, Ct);
        var extendedVersion = await SavedVersion.ReadAsync(extended, Ct);
        Assert.Equal((true, "Trial", extendedVersion), await ReadTabRowAsync(client, path));
        Assert.Equal(subscription, await ReadRowAsync(host.Services, tenantId, TenantFeatureSource.Subscription));
        var manual = await ReadRowAsync(host.Services, tenantId, TenantFeatureSource.Manual);
        Assert.Equal((false, manualVersion), (manual.Enabled, manual.Version));
    }

    private static async Task AssertBusinessModuleAsync(HttpClient client, bool enabled)
    {
        using var gate = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(enabled ? HttpStatusCode.OK : HttpStatusCode.NotFound, gate.StatusCode);
        using var me = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var features = me!.RootElement.GetProperty("features").EnumerateArray().Select(row => row.GetString()).ToArray();
        Assert.Equal(enabled, features.Contains("test-business"));
    }

    private static async Task<bool> IsWhatsAppEnabledAsync(IServiceProvider provider, Guid organizationId)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WhatsAppModulePolicy>().IsEnabledAsync(organizationId, Ct);
    }

    private static async Task<(bool Enabled, string? Source, uint Version)> ReadTabRowAsync(HttpClient client, string path)
    {
        using var rows = await client.GetFromJsonAsync<JsonDocument>(path, Ct);
        var row = rows!.RootElement.EnumerateArray().Single(item => item.GetProperty("key").GetString() == "test-business");
        return (row.GetProperty("enabled").GetBoolean(), row.GetProperty("source").GetString(), row.GetProperty("version").GetUInt32());
    }

    private static async Task<(bool Enabled, uint Version, DateTime? ExpiresAtUtc)> ReadRowAsync(IServiceProvider provider, Guid tenantId,
        TenantFeatureSource source)
    {
        await using var scope = provider.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TenantFeatures.AsNoTracking()
            .SingleAsync(item => item.TenantId == tenantId && item.FeatureKey == "test-business" && item.Source == source, Ct);
        return (row.Enabled, row.Version, row.ExpiresAtUtc);
    }

    /// <summary>Carga una fila directo en la base, con su evento en el mismo commit, como haría el writer de su origen.</summary>
    private static async Task AddAsync(IServiceProvider provider, Guid tenantId, string key, bool enabled, TenantFeatureSource source,
        DateTime? expiresAtUtc)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, key, enabled, source, expiresAtUtc).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty,
                tenantId, "Fila cargada por el test", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess);
    }

    private static async Task<Guid> CreateBusinessAsync(IServiceProvider provider, string name)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var owner = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            Result.Success((await services.GetRequiredService<IUserRepository>().CreateAsync("Dueña de " + name, "es-AR",
                "America/Argentina/Buenos_Aires", ct)).Id), CommitPolicy.OnSuccess, Ct);
        var organization = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(organization.Activate().IsSuccess);
        using var entered = services.GetRequiredService<ITenantScope>().Enter(organization.Id);
        var staged = await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(organization,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.Value], owner.Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(staged.IsSuccess);
        return organization.Id;
    }
}
