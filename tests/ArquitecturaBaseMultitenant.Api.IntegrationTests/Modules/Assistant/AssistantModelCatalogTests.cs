using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantModelCatalogTests
{
    [Fact]
    public async Task Catalog_edits_survive_noop_seed_and_disabled_currency_remains_readable()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        using var list = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?page=1&pageSize=20", AssistantTestData.Ct);
        var model = list!.RootElement.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("providerModelId").GetString() == "claude-sonnet-5-5");
        var id = model.GetProperty("id").GetGuid();
        var originalVersion = model.GetProperty("version").GetUInt32();
        using var changed = await client.PutAsJsonAsync($"/api/platform/assistant/models/{id}", new
        {
            name = "Precio editado", provider = "Anthropic", providerModelId = "claude-sonnet-5-5", inputPricePerMillion = new { amount = 3.125m, currency = "USD" },
            outputPricePerMillion = new { amount = 12m, currency = "USD" }, isActive = true, version = originalVersion,
        }, AssistantTestData.Ct);
        var version = await SavedVersion.ReadAsync(changed, AssistantTestData.Ct);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var currency = await db.Set<Currency>().SingleAsync(row => row.Code == "USD", ct);
                db.Entry(currency).Property(row => row.IsEnabled).CurrentValue = false;
                foreach (var contributor in services.GetServices<IModuleDataSeedContributor>()) await contributor.StageAsync(ct);
                return Domain.Results.Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
            await services.GetRequiredService<ReferenceDataCache>().InvalidateAsync(AssistantTestData.Ct);
        }
        using var after = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?page=1&pageSize=20", AssistantTestData.Ct);
        var saved = after!.RootElement.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == id);
        Assert.Equal(3.125m, saved.GetProperty("inputPricePerMillion").GetProperty("amount").GetDecimal());
        Assert.Equal("Precio editado", saved.GetProperty("name").GetString());
        Assert.Equal(version, saved.GetProperty("version").GetUInt32());
        using var configuration = await client.GetAsync("/api/platform/assistant/settings", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.OK, configuration.StatusCode);
        using var denied = await AccountJourney.PostAsync(client, "/api/platform/assistant/models", new
        {
            name = "Dato nuevo", provider = "Anthropic", providerModelId = "unknown-model", inputPricePerMillion = new { amount = 1m, currency = "USD" },
            outputPricePerMillion = new { amount = 2m, currency = "USD" }, isActive = true,
        }, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
    }

    [Fact]
    public async Task Additional_versioned_reference_allows_catalog_creation_and_own_selection_without_being_platform_allowed()
    {
        var file = Path.Combine(Path.GetTempPath(), "assistant-catalog-" + Guid.CreateVersion7().ToString("N") + ".json");
        try
        {
            File.WriteAllText(file, """
                [{"currency":"USD","revision":"isolated-fixture-v2","providerModelId":"isolated-compatible-model","nameEs":"Prueba","nameEn":"Test","inputPerMillion":1,"outputPerMillion":2,"cacheWrite5MinutesPerMillion":1.25,"cacheWrite1HourPerMillion":2,"cacheReadPerMillion":0.1,"text":true,"images":true,"pdf":true,"tools":true,"efforts":["Low"]}]
                """);
            await using var factory = new ApiFactory();
            await factory.InitializeAsync();
            using var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Assistant:ProviderReferenceFile", file);
                builder.UseSetting("Assistant:UseFakeChatClient", "true");
            });
            using var ownerClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var owner = await AccountJourney.RegisterAsync(host, ownerClient, AssistantTestData.Ct);
            var tenant = await OrganizationJourney.StageAsync(factory, owner, "Additional model", AssistantTestData.Ct);
            await AccountJourney.AuthorizeAsync(ownerClient, "business", AssistantTestData.Ct, tenant);
            await AssistantTestData.EnableAsync(factory, tenant);
            using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var account = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
            await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
            await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
            using var created = await AccountJourney.PostAsync(client, "/api/platform/assistant/models", new
            {
                name = "Modelo adicional", provider = "Anthropic", providerModelId = "isolated-compatible-model", inputPricePerMillion = new { amount = 0.125m, currency = "USD" },
                outputPricePerMillion = new { amount = 2m, currency = "USD" }, isActive = true,
            }, AssistantTestData.Ct);
            Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync(AssistantTestData.Ct));
            using var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync(AssistantTestData.Ct));
            var id = result.RootElement.GetProperty("id").GetGuid();
            using var allowed = await client.PutAsJsonAsync($"/api/platform/tenants/{tenant}/assistant/policy", new { allowOwnKey = true, version = 0, reason = "Additional model fixture" }, AssistantTestData.Ct);
            await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
            using var settings = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/organization/assistant/settings", AssistantTestData.Ct);
            Assert.Contains(settings!.RootElement.GetProperty("ownKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
            Assert.Equal(0.125m, settings.RootElement.GetProperty("ownKeyModels").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == id).GetProperty("inputPricePerMillion").GetProperty("amount").GetDecimal());
            Assert.DoesNotContain(settings.RootElement.GetProperty("platformKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
            using var selected = await ownerClient.PutAsJsonAsync("/api/organization/assistant/settings", new
            {
                mode = "OwnKey", modelId = id, effort = "low", monthlyBudget = new { amount = 0.125m, currency = "USD" }, keyAction = "replace", apiKey = "isolated-own-key-9999", version = 0,
            }, AssistantTestData.Ct);
            await SavedVersion.ReadAsync(selected, AssistantTestData.Ct);
            using var saved = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/organization/assistant/settings", AssistantTestData.Ct);
            Assert.Equal(id, saved!.RootElement.GetProperty("modelId").GetGuid());
            Assert.Equal(0.13m, saved.RootElement.GetProperty("monthlyBudget").GetProperty("amount").GetDecimal());
        }
        finally { File.Delete(file); }
    }
}
