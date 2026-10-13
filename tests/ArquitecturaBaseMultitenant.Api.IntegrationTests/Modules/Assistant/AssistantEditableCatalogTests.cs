using Microsoft.AspNetCore.TestHost;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantEditableCatalogTests
{
    [Fact]
    public async Task Newly_created_model_is_immediately_selectable_from_database_without_a_reference_file()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting("Assistant:UseFakeChatClient", "true").ConfigureTestServices(AssistantFakeChatRegistration.Configure));
        using var ownerClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, ownerClient, AssistantTestData.Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Editable catalog", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(ownerClient, "business", AssistantTestData.Ct, tenant);
        await AssistantTestData.EnableAsync(factory, tenant);
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        using var created = await AccountJourney.PostAsync(client, "/api/platform/assistant/models", new
        {
            name = "Modelo editable", provider = "Anthropic", providerModelId = "isolated-editable-model",
            inputPricePerMillion = new { amount = 0.123456m, currency = "USD" },
            outputPricePerMillion = new { amount = 2m, currency = "USD" }, isActive = true, technical = Technical(),
        }, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync(AssistantTestData.Ct));
        var id = result.RootElement.GetProperty("id").GetGuid();
        using var allowed = await client.PutAsJsonAsync($"/api/platform/tenants/{tenant}/assistant/policy",
            new { allowOwnKey = true, version = 0, reason = "Editable catalog test" }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
        using var settings = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/organization/assistant/settings", AssistantTestData.Ct);
        Assert.Contains(settings!.RootElement.GetProperty("ownKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        Assert.DoesNotContain(settings.RootElement.GetProperty("platformKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        using var selected = await ownerClient.PutAsJsonAsync("/api/organization/assistant/settings", new
        {
            mode = "OwnKey", modelId = id, effort = "low", monthlyBudget = new { amount = 0.125m, currency = "USD" },
            keyAction = "replace", apiKey = "isolated-own-key-9999", version = 0,
        }, AssistantTestData.Ct);
        var organizationVersion = await SavedVersion.ReadAsync(selected, AssistantTestData.Ct);
        var ownConfiguration = await ResolveAsync(host, tenant, owner.UserId);
        Assert.Equal(id, ownConfiguration.Model.Id);
        Assert.Equal(AssistantMode.OwnKey, ownConfiguration.Mode);
        Assert.Equal(.123456m, ownConfiguration.Reference.InputPerMillion.Amount);
        using var platform = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/settings", AssistantTestData.Ct);
        using var permitted = await client.PutAsJsonAsync("/api/platform/assistant/settings", new
        {
            defaultModelId = id, allowedModelIds = new[] { id }, effort = "low", dailyMessageLimit = 50,
            monthlyOrganizationBudget = new { amount = 100m, currency = "USD" }, personalEnabled = false,
            retentionDays = 30, keyAction = "replace", apiKey = "isolated-platform-key-9999",
            version = platform!.RootElement.GetProperty("version").GetUInt32(),
        }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(permitted, AssistantTestData.Ct);
        using var available = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/organization/assistant/settings", AssistantTestData.Ct);
        Assert.Contains(available!.RootElement.GetProperty("platformKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        using var platformSelection = await ownerClient.PutAsJsonAsync("/api/organization/assistant/settings", new
        { mode = "PlatformKey", modelId = id, effort = "low", keyAction = "keep", version = organizationVersion }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(platformSelection, AssistantTestData.Ct);
        Assert.Equal(AssistantMode.PlatformKey, (await ResolveAsync(host, tenant, owner.UserId)).Mode);

        var operationId = Guid.CreateVersion7();
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
                var snapshot = AssistantPriceSnapshotBuilder.Build(ownConfiguration.Model, ownConfiguration.Reference);
                services.GetRequiredService<ApplicationDbContext>().Add(AssistantFinancialOperation.Create(operationId,
                    new(account.UserId, Access.Platform, null), AssistantKeyFunding.PlatformKey, DateOnly.FromDateTime(now),
                    50, null, null, new Money(.000000123456m, CurrencyCode.Create("USD").Value), snapshot, null, now));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var catalog = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?pageSize=20", AssistantTestData.Ct);
        var row = catalog!.RootElement.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == id);
        var edit = JsonNode.Parse(row.GetRawText())!.AsObject();
        edit.Remove("id"); edit.Remove("technicalRevision");
        edit["providerModelId"] = "renamed-editable-model";
        edit["technical"]!["cacheReadPricePerMillion"]!["amount"] = .123456m;
        edit["technical"]!["supportsPdf"] = false;
        edit["technical"]!["longContext"] = JsonSerializer.SerializeToNode(new
        {
            inputTokensThreshold = 75000, inputPricePerMillion = new { amount = .234567m, currency = "USD" },
            outputPricePerMillion = new { amount = 3.456789m, currency = "USD" },
            cacheWrite5MinutesPricePerMillion = new { amount = .345678m, currency = "USD" },
            cacheWrite1HourPricePerMillion = new { amount = .456789m, currency = "USD" },
            cacheReadPricePerMillion = new { amount = .045678m, currency = "USD" },
        });
        using var edited = await client.PutAsJsonAsync($"/api/platform/assistant/models/{id}", edit, AssistantTestData.Ct);
        var version = await SavedVersion.ReadAsync(edited, AssistantTestData.Ct);
        var changed = await ResolveAsync(host, tenant, owner.UserId);
        Assert.Equal("renamed-editable-model", changed.Model.ProviderModelId);
        Assert.Equal(.123456m, changed.Reference.CacheReadPerMillion.Amount);
        Assert.Equal(.045678m, changed.Reference.LongCacheReadPerMillion!.Amount);
        Assert.NotEqual(ownConfiguration.Reference.Revision, changed.Reference.Revision);
        using var stale = await client.PutAsJsonAsync($"/api/platform/assistant/models/{id}", edit, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        edit["version"] = version;
        var invalid = edit.DeepClone();
        invalid["technical"]!["cacheReadPricePerMillion"]!["amount"] = .1234567m;
        using var rejected = await client.PutAsJsonAsync($"/api/platform/assistant/models/{id}", invalid, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var historical = await db.Set<AssistantFinancialOperation>().AsNoTracking().SingleAsync(value => value.Id == operationId, AssistantTestData.Ct);
            Assert.Equal(ownConfiguration.Reference.Revision, historical.Price.Revision);
            Assert.Equal("isolated-editable-model", historical.Price.ProviderModelId);
            Assert.Equal(.01m, historical.Price.CacheRead.Amount);
            Assert.Equal(.000000123456m, historical.ReservedCost.Amount);
            Assert.Equal(1, await db.SecurityEvents.CountAsync(value => value.ActorId == account.UserId && value.Reason == "Assistant model updated", AssistantTestData.Ct));
            Assert.Equal(version, (await scope.ServiceProvider.GetRequiredService<IAssistantModelReader>().ListAllAsync(AssistantTestData.Ct)).Single(value => value.Id == id).Version);
        }
        // A coherent small model stays in administration but cannot become a runtime configuration.
        edit["technical"]!["longContext"] = null;
        edit["technical"]!["maxInputTokens"] = 1000;
        edit["technical"]!["maxOutputTokens"] = 500;
        using var small = await client.PutAsJsonAsync($"/api/platform/assistant/models/{id}", edit, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(small, AssistantTestData.Ct);
        using var after = await ownerClient.GetFromJsonAsync<JsonDocument>("/api/organization/assistant/settings", AssistantTestData.Ct);
        Assert.DoesNotContain(after!.RootElement.GetProperty("platformKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        Assert.DoesNotContain(after.RootElement.GetProperty("ownKeyModels").EnumerateArray(), row => row.GetProperty("id").GetGuid() == id);
        using var resolutionScope = host.Services.CreateScope();
        using var entered = resolutionScope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        Assert.Equal(AssistantErrors.ModelNotAllowedCode, (await Policy(resolutionScope.ServiceProvider, owner.UserId).ResolveAsync(AssistantTestData.Ct)).Error.Code);
    }

    [Fact]
    public async Task Configure_without_use_or_key_reads_legacy_and_validates_complete_metadata_without_partial_audit()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Configure], AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        var price = new Money(.123456m, CurrencyCode.Create("USD").Value);
        var legacy = AssistantModelPrice.Create("Anthropic", "legacy-unlisted", "Legacy", price, price);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                services.GetRequiredService<ApplicationDbContext>().Add(legacy);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System,
                    Guid.Empty, "Legacy metadata fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var list = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?pageSize=20", AssistantTestData.Ct);
        var original = list!.RootElement.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("id").GetGuid() == legacy.Id);
        Assert.Equal(JsonValueKind.Null, original.GetProperty("technical").ValueKind);
        Assert.Equal(JsonValueKind.Null, original.GetProperty("technicalRevision").ValueKind);
        var body = JsonNode.Parse(original.GetRawText())!.AsObject();
        body.Remove("id"); body.Remove("technicalRevision");
        foreach (var language in new[] { "es", "en" })
        {
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
            foreach (var mutation in new[] { "missing", "efforts", "capability", "precision", "currency", "tier" })
            {
                var invalid = body.DeepClone();
                invalid["technical"] = JsonSerializer.SerializeToNode(Technical());
                switch (mutation)
                {
                    case "missing": invalid["technical"] = null; break;
                    case "efforts": invalid["technical"]!["efforts"] = new JsonArray("low", "low"); break;
                    case "capability": invalid["technical"]!.AsObject().Remove("supportsText"); break;
                    case "precision": invalid["technical"]!["cacheReadPricePerMillion"]!["amount"] = .1234567m; break;
                    case "currency": invalid["technical"]!["cacheReadPricePerMillion"]!["currency"] = "EUR"; break;
                    case "tier": invalid["technical"]!["longContext"] = new JsonObject { ["inputTokensThreshold"] = 50000 }; break;
                }
                using var rejected = await client.PutAsJsonAsync($"/api/platform/assistant/models/{legacy.Id}", invalid, AssistantTestData.Ct);
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(AssistantTestData.Ct));
                Assert.Contains(problem.RootElement.GetProperty("errors").EnumerateObject(), field => field.Name.StartsWith("technical", StringComparison.Ordinal));
            }
            client.DefaultRequestHeaders.AcceptLanguage.Clear();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(original.GetProperty("version").GetUInt32(), (await db.Set<AssistantModelPrice>().AsNoTracking().SingleAsync(value => value.Id == legacy.Id, AssistantTestData.Ct)).Version);
            Assert.Equal(0, await db.SecurityEvents.CountAsync(value => value.ActorId == account.UserId && value.Reason == "Assistant model updated", AssistantTestData.Ct));
        }
        body["technical"] = JsonSerializer.SerializeToNode(Technical());
        using var completed = await client.PutAsJsonAsync($"/api/platform/assistant/models/{legacy.Id}", body, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(completed, AssistantTestData.Ct);
        using var saved = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?pageSize=20", AssistantTestData.Ct);
        var repaired = saved!.RootElement.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("id").GetGuid() == legacy.Id);
        Assert.Equal(.125m, repaired.GetProperty("technical").GetProperty("cacheWrite5MinutesPricePerMillion").GetProperty("amount").GetDecimal());
        Assert.StartsWith("db-v1-", repaired.GetProperty("technicalRevision").GetString());
        using var use = await client.GetAsync("/api/platform/assistant", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.Forbidden, use.StatusCode);
    }

    private static async Task<AssistantRuntimeConfiguration> ResolveAsync(WebApplicationFactory<Program> host, Guid tenantId, Guid userId)
    {
        using var scope = host.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var resolved = await Policy(scope.ServiceProvider, userId).ResolveAsync(AssistantTestData.Ct);
        Assert.True(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.Code : "");
        return resolved.Value;
    }
    private static AssistantConfigurationPolicy Policy(IServiceProvider services, Guid userId) => new(
        services.GetRequiredService<IAssistantSettingsReader>(), services.GetRequiredService<IAssistantModelReader>(),
        services.GetRequiredService<ITenantAdminReader>(), services.GetRequiredService<ITenantContext>(), new CurrentUser(userId));
    private sealed class CurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Access? Access => Domain.Users.Access.Business;
    }

    internal static object Technical() => new
    {
        supportsText = true, supportsTools = true, supportsImages = true, supportsPdf = true, supportsAdaptiveThinking = true,
        efforts = new[] { "low", "medium", "high" }, maxInputTokens = 100000, maxOutputTokens = 4096,
        cacheWrite5MinutesPricePerMillion = new { amount = 0.125m, currency = "USD" },
        cacheWrite1HourPricePerMillion = new { amount = 0.2m, currency = "USD" },
        cacheReadPricePerMillion = new { amount = 0.01m, currency = "USD" }, longContext = (object?)null,
    };
}
