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

    [Theory]
    [InlineData("inputTokens")]
    [InlineData("outputTokens")]
    [InlineData("cacheWriteTokens")]
    [InlineData("cacheReadTokens")]
    public void CC05_aggregate_tokens_round_trip_as_exact_numbers_above_int32(string field)
    {
        var body = Examples(Document(), "/api/platform/assistant/usage")["success"]!["value"]!["rows"]!["items"]![0]!.DeepClone();
        body[field] = 2_457_600_000L;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        var dto = JsonSerializer.Deserialize<AssistantUsageRowResponse>(body.ToJsonString(), options)!;
        var serialized = JsonSerializer.SerializeToNode(dto, options)!;
        Assert.Equal(JsonValueKind.Number, serialized[field]!.GetValueKind());
        Assert.Equal(2_457_600_000L, serialized[field]!.GetValue<long>());
        Assert.True(JsonNode.DeepEquals(body, serialized));
    }

    [Fact]
    public void CC05_only_aggregate_token_fields_use_required_int64()
    {
        var schemas = Document()["components"]!["schemas"]!;
        var row = schemas["AssistantUsageRowResponse"]!;
        foreach (var field in new[] { "inputTokens", "outputTokens", "cacheWriteTokens", "cacheReadTokens" })
        {
            Assert.Contains(field, row["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.Equal("integer", row["properties"]![field]!["type"]!.GetValue<string>());
            Assert.Equal("int64", row["properties"]![field]!["format"]!.GetValue<string>());
            Assert.Equal("int32", schemas["AssistantTurnUsageResponse"]!["properties"]![field]!["format"]!.GetValue<string>());
        }
        foreach (var (schema, field) in new[] { ("AssistantUsageRowResponse", "queries"), ("AssistantUsageRowResponse", "peopleCount"), ("AssistantUsagePageResponse", "peopleCount"), ("AssistantUsagePageResponse", "organizationCount"), ("AssistantUsagePageResponse", "totalQueries") })
            Assert.Equal("int32", schemas[schema]!["properties"]![field]!["format"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"fraction\":0,\"status\":\"WithinLimit\"}")]
    [InlineData("{\"fraction\":0.79,\"status\":\"WithinLimit\"}")]
    [InlineData("{\"fraction\":0.8,\"status\":\"NearLimit\"}")]
    [InlineData("{\"fraction\":1,\"status\":\"LimitReached\"}")]
    [InlineData("{\"fraction\":1.2,\"status\":\"LimitReached\"}")]
    public void CC06_state_round_trip_preserves_present_null_or_uncapped_monthly_progress(string? progressJson)
    {
        var body = Examples(Document(), "/api/organization/assistant")["success"]!["value"]!.DeepClone();
        body["monthlyBudgetProgress"] = progressJson is null ? null : JsonNode.Parse(progressJson);
        if (progressJson is not null)
        {
            body["configured"] = true;
            body["models"] = new JsonArray(new JsonObject { ["id"] = "10000000-0000-0000-0000-000000000001", ["name"] = "Modelo de ejemplo" });
            body["monthlyBudget"] = new JsonObject { ["amount"] = 10m, ["currency"] = "USD" };
            body["usedCost"] = new JsonObject { ["amount"] = body["monthlyBudgetProgress"]!["fraction"]!.GetValue<decimal>() * 10m, ["currency"] = "USD" };
        }
        AssertStateRoundTrip(body, progressJson ?? "null");
    }

    [Fact]
    public void CC06_state_requires_nullable_existing_progress_and_preserves_unconfigured_examples()
    {
        var document = Document();
        var schema = document["components"]!["schemas"]!["AssistantStateResponse"]!;
        Assert.Contains("monthlyBudgetProgress", schema["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        var variants = schema["properties"]!["monthlyBudgetProgress"]!["oneOf"]!.AsArray();
        Assert.Equal(2, variants.Count);
        Assert.Contains(variants, value => value?["type"]?.GetValue<string>() == "null");
        Assert.Contains(variants, value => value?["$ref"]?.GetValue<string>() == "#/components/schemas/AssistantBudgetProgressResponse");
        foreach (var side in new[] { "me", "organization", "platform" })
        {
            var examples = Examples(document, $"/api/{side}/assistant");
            var success = examples["success"]!["value"]!;
            Assert.False(success["configured"]!.GetValue<bool>());
            Assert.Null(success["monthlyBudget"]);
            Assert.Null(success["usedCost"]);
            Assert.True(success.AsObject().ContainsKey("monthlyBudgetProgress"));
            Assert.Null(success["monthlyBudgetProgress"]);
            foreach (var sample in examples.AsObject())
            {
                var body = sample.Value!["value"]!;
                Assert.True(body.AsObject().ContainsKey("monthlyBudgetProgress"), sample.Key);
                if (side != "organization") Assert.Null(body["monthlyBudgetProgress"]);
                AssertStateRoundTrip(body, sample.Key);
            }
        }
    }

    [Theory]
    [InlineData("monthlyZero", "0", "WithinLimit")]
    [InlineData("monthlyBelowNear", "0.79", "WithinLimit")]
    [InlineData("monthlyNear", "0.8", "NearLimit")]
    [InlineData("monthlyReached", "1", "LimitReached")]
    [InlineData("monthlyOver", "1.2", "LimitReached")]
    public void CC06_business_examples_use_coherent_monthly_money_and_localized_thresholds(string name, string fractionJson, string status)
    {
        var examples = Examples(Document(), "/api/organization/assistant");
        var fraction = JsonNode.Parse(fractionJson)!.GetValue<decimal>();
        foreach (var language in new[] { "es", "en" })
        {
            var sample = examples[name + "." + language];
            Assert.NotNull(sample);
            Assert.False(string.IsNullOrWhiteSpace(sample["description"]!.GetValue<string>()));
            var body = sample["value"]!;
            Assert.True(body["configured"]!.GetValue<bool>());
            var models = body["models"]!.AsArray();
            Assert.NotEmpty(models);
            Assert.All(models, model =>
            {
                Assert.NotEqual(Guid.Empty, model!["id"]!.GetValue<Guid>());
                Assert.False(string.IsNullOrWhiteSpace(model["name"]!.GetValue<string>()));
            });
            Assert.Equal("USD", body["monthlyBudget"]!["currency"]!.GetValue<string>());
            Assert.Equal(body["monthlyBudget"]!["currency"]!.GetValue<string>(), body["usedCost"]!["currency"]!.GetValue<string>());
            Assert.Equal(10m, body["monthlyBudget"]!["amount"]!.GetValue<decimal>());
            Assert.Equal(fraction * 10m, body["usedCost"]!["amount"]!.GetValue<decimal>());
            Assert.Equal(fraction, body["monthlyBudgetProgress"]!["fraction"]!.GetValue<decimal>());
            Assert.Equal(status, body["monthlyBudgetProgress"]!["status"]!.GetValue<string>());
            AssertStateRoundTrip(body, name + "." + language);
        }
    }

    private static void AssertStateRoundTrip(JsonNode body, string sample)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        var dto = JsonSerializer.Deserialize<AssistantStateResponse>(body.ToJsonString(), options)!;
        var serialized = JsonSerializer.SerializeToNode(dto, options)!;
        Assert.True(serialized.AsObject().ContainsKey("monthlyBudgetProgress"), sample);
        Assert.True(JsonNode.DeepEquals(body, serialized), sample);
    }

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
        var row = new AssistantUsageRowResponse(AssistantUsageRowKind.Person, null, Guid.NewGuid(), null, "Persona", 100, 0, 0, 0, 0, money, 50, money, null, null, null, null);
        var page = new AssistantUsagePageResponse("2026-10", money, null, money, 100, 1, new([row], 1, 20, 1), null, null);
        var absent = JsonSerializer.SerializeToNode(page, options)!;
        Assert.True(absent.AsObject().ContainsKey("budgetProgress"));
        Assert.Null(absent["budgetProgress"]);
        Assert.True(absent["rows"]!["items"]![0]!.AsObject().ContainsKey("dailyUsage"));
        Assert.Null(absent["rows"]!["items"]![0]!["dailyUsage"]);
        Assert.True(absent.AsObject().ContainsKey("organizationCount"));
        Assert.Null(absent["organizationCount"]);
        foreach (var field in new[] { "peopleCount", "fundingMode", "budgetProgress" })
        {
            Assert.True(absent["rows"]!["items"]![0]!.AsObject().ContainsKey(field), field);
            Assert.Null(absent["rows"]!["items"]![0]![field]);
        }
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

    [Theory]
    [InlineData("AssistantUsageRowResponse", "peopleCount", "integer")]
    [InlineData("AssistantUsagePageResponse", "organizationCount", "integer")]
    [InlineData("AssistantUsageRowResponse", "fundingMode", "AssistantMode")]
    [InlineData("AssistantUsageRowResponse", "budgetProgress", "AssistantBudgetProgressResponse")]
    public void CC04_fields_are_required_nullable_and_reuse_closed_existing_types(string name, string field, string type)
    {
        var schemas = Document()["components"]!["schemas"]!;
        var schema = schemas[name]!;
        Assert.Contains(field, schema["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        var property = schema["properties"]![field]!;
        if (type == "integer")
        {
            Assert.Equal(new[] { "integer", "null" }, property["type"]!.AsArray().Select(value => value!.GetValue<string>()).Order());
            Assert.Equal("int32", property["format"]!.GetValue<string>());
        }
        else
        {
            var variants = property["oneOf"]!.AsArray();
            Assert.Contains(variants, value => value?["type"]?.GetValue<string>() == "null");
            Assert.Contains(variants, value => value?["$ref"]?.GetValue<string>() == "#/components/schemas/" + type);
        }
        Assert.Equal(new[] { "PlatformKey", "OwnKey" }, schemas["AssistantMode"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()));
        foreach (var token in new[] { "inputTokens", "outputTokens", "cacheWriteTokens", "cacheReadTokens" })
            Assert.Equal("int64", schemas["AssistantUsageRowResponse"]!["properties"]![token]!["format"]!.GetValue<string>());
    }

    [Fact]
    public void CC04_business_examples_distinguish_daily_people_from_distinct_people_in_the_period()
    {
        var examples = Examples(Document(), "/api/organization/assistant/usage");
        foreach (var sample in examples.AsObject())
        {
            var body = sample.Value!["value"]!;
            Assert.True(body.AsObject().ContainsKey("organizationCount"));
            Assert.Null(body["organizationCount"]);
            foreach (var row in body["rows"]!["items"]!.AsArray())
            {
                Assert.True(row!.AsObject().ContainsKey("fundingMode"));
                Assert.Null(row["fundingMode"]);
                Assert.True(row.AsObject().ContainsKey("budgetProgress"));
                Assert.Null(row["budgetProgress"]);
                Assert.True(row.AsObject().ContainsKey("peopleCount"));
                if (row["kind"]!.GetValue<string>() == "person") Assert.Null(row["peopleCount"]);
            }
            AssertUsageRoundTrip(body, sample.Key);
        }
        var days = examples["dailyPeople"]!["value"]!;
        var rows = days["rows"]!["items"]!.AsArray();
        Assert.Equal(3, days["peopleCount"]!.GetValue<int>());
        Assert.Equal(new[] { 2, 2, 0 }, rows.Select(row => row!["peopleCount"]!.GetValue<int>()));
        Assert.Equal(new[] { 5, 3, 0 }, rows.Select(row => row!["queries"]!.GetValue<int>()));
        Assert.All(rows, row => { Assert.Equal("day", row!["kind"]!.GetValue<string>()); Assert.Null(row["dailyUsage"]); });
        Assert.Contains("ambas claves", examples["dailyPeople"]!["description"]!.GetValue<string>());
        var empty = examples["zeroUsage"]!["value"]!;
        Assert.Equal(0, empty["peopleCount"]!.GetValue<int>());
        Assert.Empty(empty["rows"]!["items"]!.AsArray());
    }

    [Fact]
    public void CC04_platform_examples_keep_funding_costs_snapshots_and_full_period_counts_separate()
    {
        var examples = Examples(Document(), "/api/platform/assistant/usage");
        var mixed = examples["success"]!["value"]!;
        var rows = mixed["rows"]!["items"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0]!["organizationId"]!.GetValue<Guid>(), rows[1]!["organizationId"]!.GetValue<Guid>());
        Assert.Equal(new[] { "PlatformKey", "OwnKey" }, rows.Select(row => row!["fundingMode"]!.GetValue<string>()));
        Assert.Equal(new[] { 20m, 7m }, rows.Select(row => row!["cost"]!["amount"]!.GetValue<decimal>()));
        Assert.Equal(new[] { 10, 3 }, rows.Select(row => row!["queries"]!.GetValue<int>()));
        Assert.Equal(1, mixed["organizationCount"]!.GetValue<int>());
        Assert.Equal(2, mixed["rows"]!["totalCount"]!.GetValue<int>());
        Assert.Equal(27m, mixed["cost"]!["amount"]!.GetValue<decimal>());
        Assert.Equal(20m, mixed["platformKeyCost"]!["amount"]!.GetValue<decimal>());
        Assert.Equal(13, mixed["totalQueries"]!.GetValue<int>());
        Assert.Equal(new[] { 0.8m, 1m }, rows.Select(row => row!["budgetProgress"]!["fraction"]!.GetValue<decimal>()));
        var historical = examples["historicalFunding"]!["value"]!;
        Assert.Equal("2026-09", historical["month"]!.GetValue<string>());
        Assert.Equal(1.2m, historical["rows"]!["items"]![0]!["budgetProgress"]!["fraction"]!.GetValue<decimal>());
        Assert.Contains("snapshot", examples["historicalFunding"]!["description"]!.GetValue<string>());
        var page = examples["paginatedFunding"]!["value"]!;
        Assert.Equal(10, page["organizationCount"]!.GetValue<int>());
        Assert.Equal(11, page["rows"]!["totalCount"]!.GetValue<int>());
        Assert.Equal(2, page["rows"]!["page"]!.GetValue<int>());
        Assert.Single(page["rows"]!["items"]!.AsArray());
        Assert.Equal(36m, page["cost"]!["amount"]!.GetValue<decimal>());
        Assert.Equal(29m, page["platformKeyCost"]!["amount"]!.GetValue<decimal>());
        var aggregates = examples["aggregates"]!["value"]!;
        Assert.Equal(0, aggregates["organizationCount"]!.GetValue<int>());
        Assert.Equal(new[] { "personal", "operator" }, aggregates["rows"]!["items"]!.AsArray().Select(row => row!["kind"]!.GetValue<string>()));
        foreach (var sample in examples.AsObject())
        {
            var body = sample.Value!["value"]!;
            Assert.Null(body["budgetProgress"]);
            foreach (var row in body["rows"]!["items"]!.AsArray())
            {
                Assert.Null(row!["peopleCount"]);
                Assert.Null(row["dailyUsage"]);
                Assert.Contains(row["fundingMode"]!.GetValue<string>(), new[] { "PlatformKey", "OwnKey" });
                var budget = row["monthlyBudget"]?["amount"]?.GetValue<decimal>();
                if (row["kind"]!.GetValue<string>() != "organization" || budget is null or <= 0) Assert.Null(row["budgetProgress"]);
                else
                {
                    var fraction = row["cost"]!["amount"]!.GetValue<decimal>() / budget.Value;
                    Assert.Equal(fraction, row["budgetProgress"]!["fraction"]!.GetValue<decimal>());
                    Assert.Equal(fraction >= 1m ? "LimitReached" : fraction >= 0.8m ? "NearLimit" : "WithinLimit", row["budgetProgress"]!["status"]!.GetValue<string>());
                }
            }
            AssertUsageRoundTrip(body, sample.Key);
        }
        var noBudget = examples["budgetsNotApplicable"]!["value"]!["rows"]!["items"]!.AsArray();
        Assert.Equal(0m, noBudget[0]!["monthlyBudget"]!["amount"]!.GetValue<decimal>());
        Assert.Null(noBudget[1]!["monthlyBudget"]);
        Assert.All(noBudget, row => Assert.Null(row!["budgetProgress"]));
        var empty = examples["zeroUsage"]!["value"]!;
        Assert.Equal(0, empty["organizationCount"]!.GetValue<int>());
        Assert.Empty(empty["rows"]!["items"]!.AsArray());
    }

    private static void AssertUsageRoundTrip(JsonNode body, string sample)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfiguration.ConfigureJson(options);
        var dto = JsonSerializer.Deserialize<AssistantUsagePageResponse>(body.ToJsonString(), options)!;
        Assert.True(JsonNode.DeepEquals(body, JsonSerializer.SerializeToNode(dto, options)), sample);
        var rows = body["rows"]!;
        var items = rows["items"]!.AsArray();
        if (items.Count == rows["totalCount"]!.GetValue<int>())
        {
            Assert.Equal(body["cost"]!["amount"]!.GetValue<decimal>(), items.Sum(row => row!["cost"]!["amount"]!.GetValue<decimal>()));
            Assert.Equal(body["totalQueries"]!.GetValue<int>(), items.Sum(row => row!["queries"]!.GetValue<int>()));
            if (body["organizationCount"] is not null)
            {
                Assert.Equal(body["organizationCount"]!.GetValue<int>(), items.Where(row => row!["kind"]!.GetValue<string>() == "organization").Select(row => row!["organizationId"]!.GetValue<Guid>()).Distinct().Count());
                Assert.Equal(body["platformKeyCost"]!["amount"]!.GetValue<decimal>(), items.Where(row => row!["fundingMode"]!.GetValue<string>() == "PlatformKey").Sum(row => row!["cost"]!["amount"]!.GetValue<decimal>()));
            }
        }
        var totalPages = (rows["totalCount"]!.GetValue<int>() + rows["pageSize"]!.GetValue<int>() - 1) / rows["pageSize"]!.GetValue<int>();
        Assert.Equal(totalPages, rows["totalPages"]!.GetValue<int>());
        Assert.Equal(rows["page"]!.GetValue<int>() > 1, rows["hasPrevious"]!.GetValue<bool>());
        Assert.Equal(rows["page"]!.GetValue<int>() < totalPages, rows["hasNext"]!.GetValue<bool>());
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
        var ownKeyAllowed = examples["ownKeyAllowed"]!["value"]!;
        Assert.Equal("PlatformKey", ownKeyAllowed["mode"]!.GetValue<string>());
        Assert.True(ownKeyAllowed["allowOwnKey"]!.GetValue<bool>());
        Assert.NotEmpty(ownKeyAllowed["ownKeyModels"]!.AsArray());
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
