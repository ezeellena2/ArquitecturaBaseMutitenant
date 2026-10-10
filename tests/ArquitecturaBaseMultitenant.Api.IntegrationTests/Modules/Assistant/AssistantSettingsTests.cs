using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantSettingsTests(ApiFactory factory)
{
    [Fact]
    public async Task Own_limits_and_key_survive_platform_mode_with_live_global_limits_and_omitted_own_limits()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var probe = new ValidationProbe();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(provider => new DraftClientFactory(provider.GetRequiredService<ApplicationDbContext>(), probe));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var operatorClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
        var tenant = await OrganizationJourney.StageAsync(isolated, owner, "Own limits roundtrip", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, tenant);
        await AssistantTestData.EnableAsync(isolated, tenant);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", AssistantTestData.Ct);
        using var allowed = await operatorClient.PutAsJsonAsync($"/api/platform/tenants/{tenant}/assistant/policy", new { allowOwnKey = true, version = 0, reason = "Own limits fixture" }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
        using var initial = await ReadAsync(client, "/api/organization/assistant/settings");
        var model = initial.RootElement.GetProperty("platformKeyModels")[0].GetProperty("id").GetGuid();
        uint version = 0;
        foreach (var amount in new[] { 25m, 0m })
        {
            using var own = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
            {
                mode = "OwnKey", modelId = model, effort = "low", dailyMessageLimit = 8,
                monthlyBudget = new { amount, currency = "USD" }, keyAction = version == 0 ? "replace" : "keep",
                apiKey = version == 0 ? "isolated-roundtrip-8888" : null, version,
            }, AssistantTestData.Ct);
            version = await SavedVersion.ReadAsync(own, AssistantTestData.Ct);
            using var global = await ReadAsync(operatorClient, "/api/platform/assistant/settings");
            var globalBody = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(PlatformBody(global, global.RootElement.GetProperty("version").GetUInt32(), "keep")))!;
            globalBody["dailyMessageLimit"] = 70;
            globalBody["monthlyOrganizationBudget"]!["amount"] = 150m;
            globalBody["retentionDays"] = 45;
            using var changed = await operatorClient.PutAsJsonAsync("/api/platform/assistant/settings", globalBody, AssistantTestData.Ct);
            await SavedVersion.ReadAsync(changed, AssistantTestData.Ct);
            using var ownWithLiveRetention = await ReadAsync(client, "/api/organization/assistant/settings");
            Assert.Equal(45, ownWithLiveRetention.RootElement.GetProperty("retentionDays").GetInt32());
            Assert.Equal(8, ownWithLiveRetention.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            using var switched = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
            { mode = "PlatformKey", modelId = model, effort = "low", keyAction = "keep", version }, AssistantTestData.Ct);
            version = await SavedVersion.ReadAsync(switched, AssistantTestData.Ct);
            using var platform = await ReadAsync(client, "/api/organization/assistant/settings");
            Assert.Equal(70, platform.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            Assert.Equal(150m, platform.RootElement.GetProperty("monthlyBudget").GetProperty("amount").GetDecimal());
            if (amount == 0) await SetUsdEnabledAsync(host, false);
            using var restored = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
            { mode = "OwnKey", modelId = model, effort = "low", keyAction = "keep", version }, AssistantTestData.Ct);
            version = await SavedVersion.ReadAsync(restored, AssistantTestData.Ct);
            using var saved = await ReadAsync(client, "/api/organization/assistant/settings");
            Assert.Equal(8, saved.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            Assert.Equal(amount, saved.RootElement.GetProperty("monthlyBudget").GetProperty("amount").GetDecimal());
            Assert.True(saved.RootElement.GetProperty("hasKey").GetBoolean());
            Assert.Equal("8888", saved.RootElement.GetProperty("maskedKeySuffix").GetString());
            Assert.Equal(version, saved.RootElement.GetProperty("version").GetUInt32());
        }
        Assert.Equal(1, probe.Validations);
    }

    [Fact]
    public async Task First_platform_row_then_own_key_without_budget_initializes_the_default_budget()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var probe = new ValidationProbe();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(provider => new DraftClientFactory(provider.GetRequiredService<ApplicationDbContext>(), probe));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var operatorClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
        var tenant = await OrganizationJourney.StageAsync(isolated, owner, "Initial platform row", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, tenant);
        await AssistantTestData.EnableAsync(isolated, tenant);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", AssistantTestData.Ct);
        using var allowed = await operatorClient.PutAsJsonAsync($"/api/platform/tenants/{tenant}/assistant/policy", new { allowOwnKey = true, version = 0, reason = "Initial Own budget fixture" }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
        using var initial = await ReadAsync(client, "/api/organization/assistant/settings");
        var model = initial.RootElement.GetProperty("platformKeyModels")[0].GetProperty("id").GetGuid();
        using var platform = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
        { mode = "PlatformKey", modelId = model, effort = "low", keyAction = "keep", version = 0 }, AssistantTestData.Ct);
        var version = await SavedVersion.ReadAsync(platform, AssistantTestData.Ct);
        await SetUsdEnabledAsync(host, false);
        using var disabledCurrency = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
        { mode = "OwnKey", modelId = model, effort = "low", keyAction = "replace", apiKey = "isolated-initial-7777", version }, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, disabledCurrency.StatusCode);
        Assert.Equal(0, probe.Validations);
        using var unchanged = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.Equal(version, unchanged.RootElement.GetProperty("version").GetUInt32());
        await SetUsdEnabledAsync(host, true);
        using var own = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
        { mode = "OwnKey", modelId = model, effort = "low", keyAction = "replace", apiKey = "isolated-initial-7777", version }, AssistantTestData.Ct);
        var ownVersion = await SavedVersion.ReadAsync(own, AssistantTestData.Ct);
        using var saved = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.Equal(initial.RootElement.GetProperty("monthlyBudget").GetProperty("amount").GetDecimal(), saved.RootElement.GetProperty("monthlyBudget").GetProperty("amount").GetDecimal());
        Assert.Equal(initial.RootElement.GetProperty("dailyMessageLimit").GetInt32(), saved.RootElement.GetProperty("dailyMessageLimit").GetInt32());
        Assert.Equal(ownVersion, saved.RootElement.GetProperty("version").GetUInt32());
        Assert.NotEqual(version, ownVersion);
        Assert.Equal(1, probe.Validations);
    }

    [Fact]
    public async Task Invalid_selection_or_effort_never_validates_a_key_and_revocation_during_validation_is_rechecked()
    {
        var file = Path.Combine(Path.GetTempPath(), "assistant-preflight-" + Guid.CreateVersion7().ToString("N") + ".json");
        try
        {
            File.WriteAllText(file, """
                [{"currency":"USD","revision":"isolated-low-only","providerModelId":"claude-sonnet-5-5","nameEs":"Prueba","nameEn":"Test","inputPerMillion":2,"outputPerMillion":10,"cacheWrite5MinutesPerMillion":2.5,"cacheWrite1HourPerMillion":4,"cacheReadPerMillion":0.2,"text":true,"images":true,"pdf":true,"tools":true,"efforts":["Low"]}]
                """);
            await using var isolated = new ApiFactory();
            await isolated.InitializeAsync();
            var probe = new ValidationProbe();
            using var host = isolated.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Assistant:ProviderReferenceFile", file);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatClientFactory>();
                    services.AddScoped<IChatClientFactory>(provider => new DraftClientFactory(provider.GetRequiredService<ApplicationDbContext>(), probe));
                });
            });
            using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var ownerClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var account = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
            await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
            await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
            var owner = await AccountJourney.RegisterAsync(host, ownerClient, AssistantTestData.Ct);
            var tenant = await OrganizationJourney.StageAsync(isolated, owner, "Key preflight", AssistantTestData.Ct);
            await AccountJourney.AuthorizeAsync(ownerClient, "business", AssistantTestData.Ct, tenant);
            await AssistantTestData.EnableAsync(isolated, tenant);
            using var allowed = await client.PutAsJsonAsync($"/api/platform/tenants/{tenant}/assistant/policy", new { allowOwnKey = true, version = 0, reason = "Preflight fixture" }, AssistantTestData.Ct);
            await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
            using var initial = await ReadAsync(client, "/api/platform/assistant/settings");
            var version = initial.RootElement.GetProperty("version").GetUInt32();
            var model = initial.RootElement.GetProperty("defaultModelId").GetGuid();
            foreach (var mutation in new[] { "effort", "default", "allowed" })
            {
                var body = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(PlatformBody(initial, version, "replace", "isolated-preflight-6666")))!;
                if (mutation == "effort") body["effort"] = "high";
                if (mutation == "default") body["defaultModelId"] = Guid.CreateVersion7();
                if (mutation == "allowed") body["allowedModelIds"] = new System.Text.Json.Nodes.JsonArray(JsonSerializer.SerializeToNode(model), JsonSerializer.SerializeToNode(Guid.CreateVersion7()));
                using var rejected = await client.PutAsJsonAsync("/api/platform/assistant/settings", body, AssistantTestData.Ct);
                await AssertModelNotAllowedAsync(rejected);
                Assert.Equal(0, probe.Validations);
            }
            foreach (var selection in new[] { model, Guid.CreateVersion7() })
            {
                using var rejected = await ownerClient.PutAsJsonAsync("/api/organization/assistant/settings", new
                { mode = "OwnKey", modelId = selection, effort = "high", keyAction = "replace", apiKey = "isolated-preflight-6666", version = 0 }, AssistantTestData.Ct);
                await AssertModelNotAllowedAsync(rejected);
                Assert.Equal(0, probe.Validations);
            }
            using var unchanged = await ReadAsync(client, "/api/platform/assistant/settings");
            Assert.Equal(version, unchanged.RootElement.GetProperty("version").GetUInt32());
            using var unchangedOwn = await ReadAsync(ownerClient, "/api/organization/assistant/settings");
            Assert.Equal(0u, unchangedOwn.RootElement.GetProperty("version").GetUInt32());
            probe.OnValidation = async ct =>
            {
                using var catalog = await client.GetFromJsonAsync<JsonDocument>("/api/platform/assistant/models?page=1&pageSize=20", ct);
                var row = catalog!.RootElement.GetProperty("items").EnumerateArray().Single(value => value.GetProperty("id").GetGuid() == model);
                using var revoked = await client.PutAsJsonAsync($"/api/platform/assistant/models/{model}", new
                {
                    name = row.GetProperty("name").GetString(), provider = "Anthropic", providerModelId = "claude-sonnet-5-5",
                    inputPricePerMillion = new { amount = 2m, currency = "USD" }, outputPricePerMillion = new { amount = 10m, currency = "USD" },
                    isActive = false, version = row.GetProperty("version").GetUInt32(),
                }, ct);
                await SavedVersion.ReadAsync(revoked, ct);
            };
            using var raced = await client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(initial, version, "replace", "isolated-preflight-6666"), AssistantTestData.Ct);
            await AssertModelNotAllowedAsync(raced);
            Assert.Equal(1, probe.Validations);
            Assert.False(probe.ObservedTransaction);
            using var final = await ReadAsync(client, "/api/platform/assistant/settings");
            Assert.Equal(version, final.RootElement.GetProperty("version").GetUInt32());
            Assert.False(final.RootElement.GetProperty("hasKey").GetBoolean());
        }
        finally { File.Delete(file); }
    }

    private static async Task AssertModelNotAllowedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(AssistantTestData.Ct));
        Assert.Equal(AssistantErrors.ModelNotAllowedCode, body.RootElement.GetProperty("code").GetString());
    }

    private static async Task SetUsdEnabledAsync(WebApplicationFactory<Program> host, bool enabled)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var currency = await db.Set<Currency>().SingleAsync(value => value.Code == "USD", ct);
            db.Entry(currency).Property(value => value.IsEnabled).CurrentValue = enabled;
            return Result.Success();
        }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        await services.GetRequiredService<ReferenceDataCache>().InvalidateAsync(AssistantTestData.Ct);
    }

    [Fact]
    public async Task Verified_owner_email_managed_by_another_active_membership_is_usable_until_membership_or_manager_is_inactive()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var ownerClient = AssistantTestData.Client(isolated);
        var owner = await AccountJourney.RegisterAsync(isolated, ownerClient, AssistantTestData.Ct);
        var target = await OrganizationJourney.StageAsync(isolated, owner, "Alert target", AssistantTestData.Ct);
        using var managerClient = AssistantTestData.Client(isolated);
        var managerOwner = await AccountJourney.RegisterAsync(isolated, managerClient, AssistantTestData.Ct);
        var manager = await OrganizationJourney.StageAsync(isolated, managerOwner, "Email manager", AssistantTestData.Ct);
        await OrganizationJourney.AddActiveMemberAsync(isolated, manager, owner.UserId, AssistantTestData.Ct);
        using (var scope = isolated.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var method = await services.GetRequiredService<ApplicationDbContext>().LoginMethods.SingleAsync(row => row.UserId == owner.UserId && row.Type == LoginMethodType.Email, ct);
                method.AssignManagedByOrganization(manager);
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
            Assert.Single(await services.GetRequiredService<ITenantAdminReader>().ListActiveVerifiedEmailRecipientsAsync(target, AssistantTestData.Ct));
        }
        using (var scope = isolated.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(manager);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var member = await services.GetRequiredService<ApplicationDbContext>().Members.SingleAsync(row => row.UserId == owner.UserId, ct);
                Assert.True(member.Deactivate().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
            Assert.Empty(await services.GetRequiredService<ITenantAdminReader>().ListActiveVerifiedEmailRecipientsAsync(target, AssistantTestData.Ct));
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var member = await db.Members.SingleAsync(row => row.UserId == owner.UserId, ct);
                Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                Assert.True((await db.Tenants.SingleAsync(row => row.Id == manager, ct)).Suspend().IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
            Assert.Empty(await services.GetRequiredService<ITenantAdminReader>().ListActiveVerifiedEmailRecipientsAsync(target, AssistantTestData.Ct));
        }
    }
    [Fact]
    public async Task Business_settings_expose_both_model_lists_without_requiring_a_provider_key()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Assistant configuration", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, tenantId);
        await AssistantTestData.EnableAsync(factory, tenantId);
        using var response = await client.GetAsync("/api/organization/assistant/settings", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(AssistantTestData.Ct));
        Assert.False(json.RootElement.GetProperty("hasKey").GetBoolean());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("platformKeyModels").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("ownKeyModels").ValueKind);
        Assert.False(json.RootElement.TryGetProperty("apiKey", out _));
    }

    [Fact]
    public async Task Platform_catalog_is_available_without_a_provider_key()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        using var response = await client.GetAsync("/api/platform/assistant/models?page=1&pageSize=20", AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(AssistantTestData.Ct));
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("items").ValueKind);
    }

    [Fact]
    public async Task Platform_keys_are_write_only_encrypted_concurrent_and_clear_prevents_startup_fallback_after_restart()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var probe = new ValidationProbe();
        using var host = isolated.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:Platform:ApiKey", "isolated-startup-1111");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(provider => new DraftClientFactory(provider.GetRequiredService<ApplicationDbContext>(), probe));
            });
        });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", AssistantTestData.Ct);
        using var initial = await ReadAsync(client, "/api/platform/assistant/settings");
        Assert.True(initial.RootElement.GetProperty("hasKey").GetBoolean());
        Assert.Equal("1111", initial.RootElement.GetProperty("maskedKeySuffix").GetString());
        var version = initial.RootElement.GetProperty("version").GetUInt32();
        foreach (var excessive in new[] { 1_000_000_000_000m, 999_999_999_999.995m })
        {
            var invalidBudget = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(PlatformBody(initial, version, "replace", "isolated-rejected-0000")))!;
            invalidBudget["monthlyOrganizationBudget"]!["amount"] = excessive;
            using var rejected = await client.PutAsJsonAsync("/api/platform/assistant/settings", invalidBudget, AssistantTestData.Ct);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        Assert.Equal(0, probe.Validations);
        using var unchanged = await ReadAsync(client, "/api/platform/assistant/settings");
        Assert.Equal(version, unchanged.RootElement.GetProperty("version").GetUInt32());
        var first = client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(initial, version, "replace", "isolated-replacement-2222"), AssistantTestData.Ct);
        var second = client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(initial, version, "replace", "isolated-replacement-3333"), AssistantTestData.Ct);
        var responses = await Task.WhenAll(first, second);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(2, probe.Validations);
        Assert.False(probe.ObservedTransaction);
        using var saved = await ReadAsync(client, "/api/platform/assistant/settings");
        var suffix = saved.RootElement.GetProperty("maskedKeySuffix").GetString();
        Assert.Contains(suffix, new[] { "2222", "3333" });
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Set<AssistantPlatformSettings>().AsNoTracking().SingleAsync(AssistantTestData.Ct);
            Assert.NotNull(row.EncryptedApiKey);
            Assert.DoesNotContain("isolated-replacement", Encoding.UTF8.GetString(row.EncryptedApiKey));
            Assert.True(row.HasKeyOverride);
            Assert.Equal(1, await db.SecurityEvents.CountAsync(value => value.ActorId == account.UserId && value.Type == SecurityEventType.PlatformSettingsChanged, AssistantTestData.Ct));
            var evidence = await db.SecurityEvents.Where(value => value.ActorId == account.UserId).Select(value => value.Changes).ToArrayAsync(AssistantTestData.Ct);
            Assert.All(evidence, value => Assert.DoesNotContain("isolated-replacement", value));
            Assert.All(evidence, value => Assert.DoesNotContain(suffix!, value));
        }
        var savedVersion = saved.RootElement.GetProperty("version").GetUInt32();
        using var kept = await client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(saved, savedVersion, "keep"), AssistantTestData.Ct);
        var keptVersion = await SavedVersion.ReadAsync(kept, AssistantTestData.Ct);
        using var keptRead = await ReadAsync(client, "/api/platform/assistant/settings");
        Assert.Equal(suffix, keptRead.RootElement.GetProperty("maskedKeySuffix").GetString());
        using var cleared = await client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(keptRead, keptVersion, "clear"), AssistantTestData.Ct);
        var clearedVersion = await SavedVersion.ReadAsync(cleared, AssistantTestData.Ct);
        using var restart = isolated.WithWebHostBuilder(builder => builder.UseSetting("Assistant:Platform:ApiKey", "isolated-restarted-4444"));
        // A second real host has its own OAuth signing keys; inspect its persisted configuration through the real reader.
        using var restartScope = restart.Services.CreateScope();
        var afterRestart = await restartScope.ServiceProvider.GetRequiredService<IAssistantSettingsReader>().FindPlatformAsync(AssistantTestData.Ct);
        Assert.False(afterRestart.HasKey);
        Assert.Null(afterRestart.MaskedKeySuffix);
        Assert.Equal(clearedVersion, afterRestart.Version);
        using var stale = await client.PutAsJsonAsync("/api/platform/assistant/settings", PlatformBody(initial, version, "keep"), AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Own_key_requires_a_verified_email_method_and_revocation_never_falls_back_to_platform_funding()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var probe = new ValidationProbe();
        using var host = isolated.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(provider => new DraftClientFactory(provider.GetRequiredService<ApplicationDbContext>(), probe));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var operatorClient = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(host, client, AssistantTestData.Ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Own key organization", AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(client, "business", AssistantTestData.Ct, tenantId);
        await AssistantTestData.EnableAsync(isolated, tenantId);
        var account = await AccountJourney.RegisterAsync(host, operatorClient, AssistantTestData.Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, AssistantTestData.Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", AssistantTestData.Ct);
        using var allowed = await operatorClient.PutAsJsonAsync($"/api/platform/tenants/{tenantId}/assistant/policy", new { allowOwnKey = true, version = 0, reason = "Own key test" }, AssistantTestData.Ct);
        var policyVersion = await SavedVersion.ReadAsync(allowed, AssistantTestData.Ct);
        using var before = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.True(before.RootElement.GetProperty("hasAlertRecipient").GetBoolean());
        Assert.Equal(2, before.RootElement.GetProperty("platformKeyModels").GetArrayLength());
        Assert.Equal(4, before.RootElement.GetProperty("ownKeyModels").GetArrayLength());
        var model = before.RootElement.GetProperty("ownKeyModels").EnumerateArray().Single(value => value.GetProperty("providerModelId").GetString() == "claude-opus-5-5").GetProperty("id").GetGuid();
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var email = await db.LoginMethods.SingleAsync(value => value.UserId == owner.UserId && value.Type == LoginMethodType.Email, ct);
                db.Remove(email);
                var phone = LoginMethod.CreatePhone(owner.UserId, PhoneNumber.Create("+5491123456789").Value);
                phone.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                db.Add(phone);
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var missing = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.False(missing.RootElement.GetProperty("hasAlertRecipient").GetBoolean());
        using var refused = await client.PutAsJsonAsync("/api/organization/assistant/settings", OwnBody(model, 0, "replace", "isolated-own-key-5555"), AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        using var refusal = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(AssistantTestData.Ct));
        Assert.Equal(AssistantErrors.SettingsAlertRecipientRequiredCode, refusal.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, probe.Validations);
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var google = LoginMethod.CreateGoogle(owner.UserId, "isolated-google-subject", owner.Email);
                google.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ApplicationDbContext>().Add(google);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var googleContact = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.False(googleContact.RootElement.GetProperty("hasAlertRecipient").GetBoolean());
        using (var scope = host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var method = LoginMethod.CreateEmail(owner.UserId, owner.Email);
                method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                services.GetRequiredService<ApplicationDbContext>().Add(method);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var replaced = await client.PutAsJsonAsync("/api/organization/assistant/settings", OwnBody(model, 0, "replace", "isolated-own-key-5555"), AssistantTestData.Ct);
        var version = await SavedVersion.ReadAsync(replaced, AssistantTestData.Ct);
        Assert.Equal(1, probe.Validations);
        Assert.False(probe.ObservedTransaction);
        using var revoked = await operatorClient.PutAsJsonAsync($"/api/platform/tenants/{tenantId}/assistant/policy", new { allowOwnKey = false, version = policyVersion, reason = "Revocation test" }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(revoked, AssistantTestData.Ct);
        using var after = await ReadAsync(client, "/api/organization/assistant/settings");
        Assert.Equal("OwnKey", after.RootElement.GetProperty("mode").GetString());
        Assert.Equal(model, after.RootElement.GetProperty("modelId").GetGuid());
        Assert.Equal(version, after.RootElement.GetProperty("version").GetUInt32());
        Assert.Empty(after.RootElement.GetProperty("ownKeyModels").EnumerateArray());
        Assert.True(after.RootElement.GetProperty("hasKey").GetBoolean());
        using var denied = await client.PutAsJsonAsync("/api/organization/assistant/settings", OwnBody(model, version, "keep"), AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var plainPlatform = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
        {
            mode = "PlatformKey", modelId = before.RootElement.GetProperty("platformKeyModels")[0].GetProperty("id").GetGuid(),
            effort = "low", keyAction = "keep", version,
        }, AssistantTestData.Ct);
        await SavedVersion.ReadAsync(plainPlatform, AssistantTestData.Ct);
        using var platformSaved = await ReadAsync(client, "/api/organization/assistant/settings");
        using var blockedLimits = await client.PutAsJsonAsync("/api/organization/assistant/settings", new
        {
            mode = "PlatformKey", modelId = before.RootElement.GetProperty("platformKeyModels")[0].GetProperty("id").GetGuid(),
            effort = "low", dailyMessageLimit = 50, keyAction = "keep", version = platformSaved.RootElement.GetProperty("version").GetUInt32(),
        }, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.BadRequest, blockedLimits.StatusCode);
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, AssistantTestData.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(AssistantTestData.Ct));
    }
    private static object PlatformBody(JsonDocument document, uint version, string keyAction, string? apiKey = null) => new
    {
        defaultModelId = document.RootElement.GetProperty("defaultModelId").GetGuid(),
        allowedModelIds = document.RootElement.GetProperty("allowedModelIds").EnumerateArray().Select(value => value.GetGuid()).ToArray(),
        effort = "low", dailyMessageLimit = 50, monthlyOrganizationBudget = new { amount = 100m, currency = "USD" },
        personalEnabled = false, retentionDays = 30, keyAction, apiKey, version,
    };
    private static object OwnBody(Guid modelId, uint version, string keyAction, string? apiKey = null) => new
    {
        mode = "OwnKey", modelId, effort = "low", dailyMessageLimit = 30, monthlyBudget = new { amount = 100m, currency = "USD" }, keyAction, apiKey, version,
    };
    private sealed class ValidationProbe
    {
        internal int Validations;
        internal bool ObservedTransaction;
        internal Func<CancellationToken, Task>? OnValidation;
    }
    private sealed class DraftClientFactory(ApplicationDbContext context, ValidationProbe probe) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("This fixture does not run chat.");
        public async Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.Validations);
            probe.ObservedTransaction |= context.Database.CurrentTransaction is not null;
            if (probe.OnValidation is not null) await probe.OnValidation(cancellationToken);
            return Result.Success();
        }
    }
}
