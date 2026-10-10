using System.Net;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantFeatureTests(ApiFactory factory)
{
    [Fact]
    public async Task Personal_setting_is_global_live_and_the_policy_does_not_enable_another_module()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(factory, false);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        await AssistantTestData.PersonalAsync(factory, true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Personal);
        Assert.False(await scope.ServiceProvider.GetRequiredService<IFeatureService>().IsEnabledAsync("unknown-module", AssistantTestData.Ct));
        await AssistantTestData.PersonalAsync(factory, false);
    }

    [Fact]
    public async Task Business_grant_uses_existing_cache_and_expires_at_the_funding_instant()
    {
        var clock = new FakeTimeProvider(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock))));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var person = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var organization = await OrganizationJourney.StageAsync(factory, person, "Assistant expiry", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, organization);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        await AssistantTestData.EnableAsync(factory, organization, clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        await host.Services.GetRequiredService<ITenantFeatureCache>().InvalidateAsync(organization, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/organization/assistant/conversations", AssistantTestData.Ct)).StatusCode);
    }

    [Fact]
    public async Task Emergency_hides_chat_but_installed_management_is_accessible_without_a_provider_key()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("FeatureManagement:assistant", "false"));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/platform/assistant/conversations", AssistantTestData.Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/platform/assistant/settings", AssistantTestData.Ct)).StatusCode);
    }
}
