using System.Text.Json.Nodes;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantApprovedContractTests
{
    private static JsonNode Document() => JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
    private static JsonNode Examples(JsonNode document, string path) => document["paths"]![path]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!;

    [Fact]
    public void Approved_projections_are_always_serialized_in_both_null_and_populated_states()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        var instantUtc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var money = new Money(20m, CurrencyCode.Create("USD").Value);
        var conversation = new AssistantConversationResponse(new(Guid.NewGuid(), null, false, 1, instantUtc, instantUtc), new([], null, false), null);
        var turnId = Guid.NewGuid();
        Assert.Null(JsonSerializer.SerializeToNode(conversation, options)!["activeTurnId"]);
        Assert.True(JsonSerializer.SerializeToNode(conversation, options)!.AsObject().ContainsKey("activeTurnId"));
        Assert.Equal(turnId, JsonSerializer.SerializeToNode(conversation with { ActiveTurnId = turnId }, options)!["activeTurnId"]!.GetValue<Guid>());
        var row = new AssistantUsageRowResponse(AssistantUsageRowKind.Person, null, Guid.NewGuid(), null, "Persona", 100, 0, 0, 0, 0, money, 50, money, null);
        var page = new AssistantUsagePageResponse("2026-10", money, null, money, 100, 1, new([row], 1, 20, 1), null);
        var absent = JsonSerializer.SerializeToNode(page, options)!;
        Assert.True(absent.AsObject().ContainsKey("budgetProgress"));
        Assert.Null(absent["budgetProgress"]);
        Assert.True(absent["rows"]!["items"]![0]!.AsObject().ContainsKey("dailyUsage"));
        Assert.Null(absent["rows"]!["items"]![0]!["dailyUsage"]);
        row = row with { DailyUsage = new(new DateOnly(2026, 10, 9), new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc), AssistantMode.PlatformKey, 49, 50, true) };
        var present = JsonSerializer.SerializeToNode(page with { BudgetProgress = new(1.2m, AssistantBudgetStatus.LimitReached), Rows = new([row], 1, 20, 1) }, options)!;
        Assert.Equal(1.2m, present["budgetProgress"]!["fraction"]!.GetValue<decimal>());
        Assert.Equal("LimitReached", present["budgetProgress"]!["status"]!.GetValue<string>());
        Assert.Equal("2026-10-10T00:00:00Z", present["rows"]!["items"]![0]!["dailyUsage"]!["resetAtUtc"]!.GetValue<string>());
        Assert.Equal("PlatformKey", present["rows"]!["items"]![0]!["dailyUsage"]!["scope"]!.GetValue<string>());
        var settings = new OrganizationAssistantSettingsResponse(AssistantMode.PlatformKey, Guid.NewGuid(), AssistantEffort.Medium, 50, null, false, false, 30, false, null, 1, [], []);
        var serializedSettings = JsonSerializer.SerializeToNode(settings, options)!;
        Assert.Empty(serializedSettings["platformKeyModels"]!.AsArray());
        Assert.Empty(serializedSettings["ownKeyModels"]!.AsArray());
    }

    [Fact]
    public void CC01_configuration_lists_are_required_priced_and_independent_of_the_saved_mode()
    {
        var document = Document();
        var schemas = document["components"]!["schemas"]!;
        var settings = schemas["OrganizationAssistantSettingsResponse"]!;
        foreach (var field in new[] { "platformKeyModels", "ownKeyModels" })
        {
            Assert.Contains(field, settings["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.Equal("array", settings["properties"]![field]!["type"]!.GetValue<string>());
            Assert.Equal("#/components/schemas/AssistantModelPriceResponse", settings["properties"]![field]!["items"]!["$ref"]!.GetValue<string>());
        }
        foreach (var field in new[] { "inputPricePerMillion", "outputPricePerMillion" })
            Assert.Equal("#/components/schemas/AssistantMoney", schemas["AssistantModelPriceResponse"]!["properties"]![field]!["$ref"]!.GetValue<string>());
        var operation = document["paths"]!["/api/organization/assistant/settings"]!["get"]!;
        Assert.Equal("business", operation["x-access"]!.GetValue<string>());
        Assert.Equal(new[] { "assistant.configure" }, operation["x-permissions"]!.AsArray().Select(value => value!.GetValue<string>()));
        var examples = Examples(document, "/api/organization/assistant/settings");
        foreach (var name in new[] { "success", "ownKeyAllowed", "noEligibleModels" })
        {
            var body = examples[name]!["value"]!;
            foreach (var field in new[] { "platformKeyModels", "ownKeyModels" })
                foreach (var model in body[field]!.AsArray())
                {
                    Assert.True(model!["isActive"]!.GetValue<bool>());
                    Assert.Equal("USD", model["inputPricePerMillion"]!["currency"]!.GetValue<string>());
                    Assert.Equal("USD", model["outputPricePerMillion"]!["currency"]!.GetValue<string>());
                }
            if (!body["allowOwnKey"]!.GetValue<bool>()) Assert.Empty(body["ownKeyModels"]!.AsArray());
            // This checks these published examples, not the validity of any saved configuration returned by GET.
            if (body["mode"]!.GetValue<string>() == "PlatformKey")
                Assert.Contains(body["platformKeyModels"]!.AsArray(), value => value!["id"]!.GetValue<string>() == body["modelId"]!.GetValue<string>());
        }
        var success = examples["success"]!["value"]!;
        Assert.Contains(success["platformKeyModels"]!.AsArray(), value => value!["id"]!.GetValue<string>() == success["modelId"]!.GetValue<string>());
        Assert.Empty(examples["noEligibleModels"]!["value"]!["platformKeyModels"]!.AsArray());
        Assert.Empty(examples["noEligibleModels"]!["value"]!["ownKeyModels"]!.AsArray());
        Assert.Equal("OwnKey", examples["noEligibleModels"]!["value"]!["mode"]!.GetValue<string>());
        Assert.False(examples["noEligibleModels"]!["value"]!["allowOwnKey"]!.GetValue<bool>());
    }

    [Fact]
    public void CC02_detail_has_a_required_nullable_uuid_for_all_private_accesses()
    {
        var document = Document();
        var schema = document["components"]!["schemas"]!["AssistantConversationResponse"]!;
        Assert.Contains("activeTurnId", schema["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        var field = schema["properties"]!["activeTurnId"]!;
        Assert.Equal(new[] { "null", "string" }, field["type"]!.AsArray().Select(value => value!.GetValue<string>()).Order());
        Assert.Equal("uuid", field["format"]!.GetValue<string>());
        foreach (var side in new[] { "me", "organization", "platform" })
        {
            var examples = Examples(document, $"/api/{side}/assistant/conversations/{{id}}");
            Assert.True(examples["success"]!["value"]!.AsObject().ContainsKey("activeTurnId"));
            Assert.Null(examples["success"]!["value"]!["activeTurnId"]);
            Assert.True(Guid.TryParse(examples["activeTurn"]!["value"]!["activeTurnId"]!.GetValue<string>(), out _));
        }
        foreach (var name in new[] { "AssistantConversationSummaryResponse", "AssistantMessageResponse" })
            Assert.Null(document["components"]!["schemas"]![name]!["properties"]!["activeTurnId"]);
    }

    [Fact]
    public void CC03_usage_projections_are_required_nullable_and_examples_distinguish_periods_and_scopes()
    {
        var document = Document();
        var schemas = document["components"]!["schemas"]!;
        foreach (var (name, field, type) in new[] { ("AssistantUsagePageResponse", "budgetProgress", "AssistantBudgetProgressResponse"), ("AssistantUsageRowResponse", "dailyUsage", "AssistantDailyUsageResponse") })
        {
            Assert.Contains(field, schemas[name]!["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            var variants = schemas[name]!["properties"]![field]!["oneOf"]!.AsArray();
            Assert.Contains(variants, value => value?["type"]?.GetValue<string>() == "null");
            Assert.Contains(variants, value => value?["$ref"]?.GetValue<string>() == "#/components/schemas/" + type);
        }
        Assert.Equal("decimal", schemas["AssistantBudgetProgressResponse"]!["properties"]!["fraction"]!["format"]!.GetValue<string>());
        Assert.Equal(new[] { "WithinLimit", "NearLimit", "LimitReached" }, schemas["AssistantBudgetStatus"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()));
        var daily = schemas["AssistantDailyUsageResponse"]!;
        Assert.Equal(6, daily["required"]!.AsArray().Count);
        Assert.Equal("date", daily["properties"]!["dayUtc"]!["format"]!.GetValue<string>());
        Assert.Equal("date-time", daily["properties"]!["resetAtUtc"]!["format"]!.GetValue<string>());
        Assert.Equal("#/components/schemas/AssistantMode", daily["properties"]!["scope"]!["$ref"]!.GetValue<string>());
        var examples = Examples(document, "/api/organization/assistant/usage");
        foreach (var (name, fraction, status, scope) in new[] { ("currentPlatformKey", 0.8m, "NearLimit", "PlatformKey"), ("currentOwnKey", 1.2m, "LimitReached", "OwnKey") })
        {
            var body = examples[name]!["value"]!;
            Assert.Equal(fraction, body["budgetProgress"]!["fraction"]!.GetValue<decimal>());
            Assert.Equal(status, body["budgetProgress"]!["status"]!.GetValue<string>());
            var row = Assert.Single(body["rows"]!["items"]!.AsArray())!;
            Assert.Equal("person", row["kind"]!.GetValue<string>());
            Assert.Equal(scope, row["dailyUsage"]!["scope"]!.GetValue<string>());
            Assert.Equal(row["dailyLimit"]!.GetValue<int>(), row["dailyUsage"]!["limit"]!.GetValue<int>());
            Assert.True(row["dailyUsage"]!["queries"]!.GetValue<int>() < row["queries"]!.GetValue<int>());
            Assert.EndsWith("Z", row["dailyUsage"]!["resetAtUtc"]!.GetValue<string>());
            Assert.True(row["dailyUsage"]!["limitReached"]!.GetValue<bool>());
        }
        Assert.Null(examples["historical"]!["value"]!["rows"]!["items"]![0]!["dailyUsage"]);
        Assert.Equal("WithinLimit", examples["historical"]!["value"]!["budgetProgress"]!["status"]!.GetValue<string>());
        var platform = Examples(document, "/api/platform/assistant/usage");
        Assert.Null(platform["success"]!["value"]!["budgetProgress"]);
        foreach (var row in platform["aggregates"]!["value"]!["rows"]!["items"]!.AsArray()) Assert.Null(row!["dailyUsage"]);
    }
}
