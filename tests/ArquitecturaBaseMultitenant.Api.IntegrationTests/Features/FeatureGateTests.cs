using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Features;

[Collection(ApiTestGroup.Name)]
public sealed class FeatureGateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Disabled_module_is_hidden_before_permissions_and_cached_trials_expire_on_time()
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
        using var anonymous = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await SampleAccountsFixture.SeedAsync(host.Services, account.Email.Value, Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct);
        using var initialMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var businessId = initialMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        Assert.Empty(initialMe.RootElement.GetProperty("features").EnumerateArray());
        using var disabled = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        using var hidden = await client.GetAsync("/test/features/no-permission", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var problem = await disabled.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("Http.NotFound", problem!.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));

        await EnableAsync(host.Services, businessId, "test-business", clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        await EnableAsync(host.Services, businessId, "test-emergency", null);
        using var enabled = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using var forbidden = await client.GetAsync("/test/features/no-permission", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var emergency = await client.GetAsync("/test/features/emergency", Ct);
        Assert.Equal(HttpStatusCode.NotFound, emergency.StatusCode);
        using var enabledMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Equal("test-business", Assert.Single(enabledMe!.RootElement.GetProperty("features").EnumerateArray()).GetString());

        clock.Advance(TimeSpan.FromMinutes(1));
        using var expired = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        using var expiredMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Empty(expiredMe!.RootElement.GetProperty("features").EnumerateArray());

        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using var consumerMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var personalId = consumerMe!.RootElement.GetProperty("activeTenantId").GetGuid();
        await EnableAsync(host.Services, personalId, "test-business", null);
        await EnableAsync(host.Services, personalId, "test-consumer", null);
        using var ownMe = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        Assert.Equal("test-consumer", Assert.Single(ownMe!.RootElement.GetProperty("features").EnumerateArray()).GetString());
        using var wrongAccess = await client.GetAsync("/test/features/business", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongAccess.StatusCode);
    }

    private static async Task EnableAsync(IServiceProvider provider, Guid tenantId, string key, DateTime? expiry)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, key, true,
                expiry is null ? TenantFeatureSource.Manual : TenantFeatureSource.Trial, expiry).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                AuditActorKind.System, Guid.Empty, tenantId, "Habilitación del test aislado", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        await services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(tenantId, Ct);
    }
}

internal sealed class TestFeatureCatalog : IFeatureCatalog
{
    public IReadOnlyList<FeatureDefinition> All { get; } =
    [
        new("test-business", FeatureScope.Business, false),
        new("test-consumer", FeatureScope.Consumer, false),
        new("test-emergency", FeatureScope.Both, true),
    ];
}
