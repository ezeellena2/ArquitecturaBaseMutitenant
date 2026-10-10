using System.Text.Json.Nodes;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantEditableCatalogContractTests
{
    [Fact]
    public void Complete_writes_nullable_legacy_reads_and_configuration_arrays_share_the_published_technical_contract()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
        var schemas = document["components"]!["schemas"]!;
        foreach (var name in new[] { "CreateAssistantModelHttpRequest", "UpdateAssistantModelHttpRequest" })
        {
            Assert.Contains("technical", schemas[name]!["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.Equal("#/components/schemas/AssistantModelTechnicalHttpRequest", schemas[name]!["properties"]!["technical"]!["$ref"]!.GetValue<string>());
        }
        foreach (var name in new[] { "AssistantModelTechnicalHttpRequest", "AssistantModelTechnicalResponse" })
        {
            var metadata = schemas[name]!;
            Assert.False(metadata["additionalProperties"]!.GetValue<bool>());
            var required = metadata["required"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
            foreach (var property in metadata["properties"]!.AsObject().Where(value => value.Key != "longContext")) Assert.Contains(property.Key, required);
            Assert.True(metadata["properties"]!["efforts"]!["uniqueItems"]!.GetValue<bool>());
            Assert.Equal(1, metadata["properties"]!["efforts"]!["minItems"]!.GetValue<int>());
            foreach (var money in metadata["properties"]!.AsObject().Where(value => value.Key.EndsWith("PricePerMillion", StringComparison.Ordinal)))
                Assert.Equal("#/components/schemas/AssistantMoney", money.Value!["$ref"]!.GetValue<string>());
        }
        var response = schemas["AssistantModelPriceResponse"]!;
        Assert.Contains(response["properties"]!["technical"]!["oneOf"]!.AsArray(), value => value?["type"]?.GetValue<string>() == "null");
        foreach (var field in new[] { "technical", "technicalRevision" }) Assert.Contains(field, response["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        foreach (var field in new[] { "platformKeyModels", "ownKeyModels" })
            Assert.Equal("#/components/schemas/AssistantModelPriceResponse", schemas["OrganizationAssistantSettingsResponse"]!["properties"]![field]!["items"]!["$ref"]!.GetValue<string>());
        foreach (var (path, method) in new[] { ("/api/platform/assistant/models", "post"), ("/api/platform/assistant/models/{id}", "put") })
        {
            var operation = document["paths"]![path]![method]!;
            var examples = operation["requestBody"]!["content"]!["application/json"]!["examples"]!;
            foreach (var language in new[] { "es", "en" })
                Assert.NotNull(examples["request." + language]!["value"]!["technical"]!["supportsAdaptiveThinking"]);
            Assert.Contains("technical.cacheReadPricePerMillion", operation.ToJsonString(), StringComparison.Ordinal);
        }
        var legacy = document["paths"]!["/api/platform/assistant/models"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["legacyIncomplete"]!["value"]!["items"]![0]!;
        Assert.Null(legacy["technical"]);
        Assert.Null(legacy["technicalRevision"]);
    }
}
