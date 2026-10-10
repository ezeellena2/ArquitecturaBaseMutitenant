using System.Text.Json.Nodes;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantContractTests
{
    [Fact]
    public void Contract_exposes_all_three_private_chat_surfaces_and_installed_management()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
        var paths = document["paths"]!.AsObject();
        foreach (var side in new[] { "me", "organization", "platform" })
        {
            var operations = paths.Where(path => path.Key.StartsWith($"/api/{side}/assistant", StringComparison.Ordinal)
                && !path.Key.Contains("/settings", StringComparison.Ordinal)
                && !path.Key.Contains("/usage", StringComparison.Ordinal)
                && !path.Key.Contains("/models", StringComparison.Ordinal))
                .Sum(path => path.Value!.AsObject().Count(operation => operation.Key is "get" or "post" or "put" or "delete"));
            Assert.Equal(15, operations);
            var send = paths[$"/api/{side}/assistant/conversations/{{id}}/messages"]!["post"]!;
            Assert.NotNull(send["responses"]!["200"]!["content"]!["text/event-stream"]);
            Assert.NotNull(send["responses"]!["200"]!["content"]!["application/json"]);
            Assert.Equal(6, send["x-sse-events"]!.AsArray().Count);
        }
        var stream = document["components"]!["schemas"]!["AssistantStreamHttpResponse"]!;
        Assert.Equal("event", stream["discriminator"]!["propertyName"]!.GetValue<string>());
        Assert.Equal(6, stream["oneOf"]!.AsArray().Count);
        Assert.NotNull(paths["/api/platform/tenants/{tenantId}/assistant/policy"]);
        var schemas = document["components"]!["schemas"]!.AsObject();
        foreach (var reference in References(document))
            if (reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                Assert.True(schemas.ContainsKey(reference["#/components/schemas/".Length..]), $"Missing schema {reference}");
        Assert.Equal(9, schemas["AssistantQuickDataResponse"]!["oneOf"]!.AsArray().Count);
        Assert.Null(schemas["AssistantQuickDataResponse"]!["discriminator"]);
        foreach (var name in new[] { "OrganizationAssistantSettingsResponse", "PlatformAssistantSettingsResponse" })
        {
            Assert.Null(schemas[name]!["properties"]!["apiKey"]);
            Assert.Null(schemas[name]!["properties"]!["encryptedKey"]);
            Assert.NotNull(schemas[name]!["properties"]!["hasKey"]);
            Assert.NotNull(schemas[name]!["properties"]!["maskedKeySuffix"]);
        }
        Assert.True(schemas["UpdatePlatformAssistantSettingsHttpRequest"]!["properties"]!["apiKey"]!["writeOnly"]!.GetValue<bool>());
    }

    [Fact]
    public void Money_queries_and_terminal_examples_have_their_closed_wire_shapes()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
        var schemas = document["components"]!["schemas"]!.AsObject();
        var money = schemas["AssistantMoney"];
        Assert.NotNull(money);
        Assert.Equal("number", money["properties"]!["amount"]!["type"]!.GetValue<string>());
        Assert.Equal("decimal", money["properties"]!["amount"]!["format"]!.GetValue<string>());
        Assert.Equal("^[A-Z]{3}$", money["properties"]!["currency"]!["pattern"]!.GetValue<string>());
        Assert.Equal(2, money["required"]!.AsArray().Count);
        Assert.False(money["additionalProperties"]!.GetValue<bool>());
        foreach (var (name, schema) in schemas.Where(pair => pair.Key.Contains("Assistant", StringComparison.Ordinal)))
            Assert.DoesNotContain("#/components/schemas/Money", References(schema));
        foreach (var (path, item) in document["paths"]!.AsObject().Where(pair => pair.Key.Contains("/assistant", StringComparison.Ordinal)))
            foreach (var (method, operation) in item!.AsObject().Where(pair => pair.Key is "get" or "post" or "put" or "delete"))
                foreach (var query in operation!["parameters"]?.AsArray().Where(parameter => parameter!["in"]!.GetValue<string>() == "query") ?? [])
                {
                    var name = query!["name"]!.GetValue<string>();
                    Assert.True(char.IsLower(name[0]), name);
                    if (name is "month" or "version") Assert.True(query["required"]!.GetValue<bool>());
                    if (name == "month") Assert.NotNull(query["schema"]!["pattern"]);
                    if (name == "page") Assert.Equal(1, query["schema"]!["default"]!.GetValue<int>());
                    if (name == "pageSize")
                    {
                        Assert.Equal(20, query["schema"]!["default"]!.GetValue<int>());
                        Assert.Equal(new[] { 10, 20, 50, 100 }, query["schema"]!["enum"]!.AsArray().Select(value => value!.GetValue<int>()));
                    }
                    if (name == "limit") Assert.Equal(path.EndsWith("/conversations", StringComparison.Ordinal) ? 20 : 50, query["schema"]!["default"]!.GetValue<int>());
                    if (name == "limit") { Assert.Equal(1, query["schema"]!["minimum"]!.GetValue<int>()); Assert.Equal(100, query["schema"]!["maximum"]!.GetValue<int>()); }
                    if (name == "format") Assert.Equal("markdown", query["schema"]!["default"]!.GetValue<string>());
                    if (name == "groupBy") Assert.Equal("day", query["schema"]!["default"]!.GetValue<string>());
                }
        foreach (var side in new[] { "me", "organization", "platform" })
        {
            var send = document["paths"]![$"/api/{side}/assistant/conversations/{{id}}/messages"]!["post"]!;
            var replay = send["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["terminalReplay"]!["value"]!;
            Assert.Equal("assistant", replay["message"]!["role"]!.GetValue<string>());
            Assert.Null(replay["message"]!["rating"]);
            Assert.Equal("Complete", replay["message"]!["status"]!.GetValue<string>());
            Assert.NotNull(replay["usage"]!["cost"]!["amount"]);
            var done = send["x-sse-events"]!.AsArray().Single(value => value!["event"]!.GetValue<string>() == "done")!["example"]!;
            Assert.Equal("assistant", done["message"]!["role"]!.GetValue<string>());
            var title = send["x-sse-events"]!.AsArray().Single(value => value!["event"]!.GetValue<string>() == "title")!["example"]!;
            Assert.NotNull(title["title"]);
            Assert.NotEmpty(title["title"]!.GetValue<string>());
            var error = send["x-sse-events"]!.AsArray().Single(value => value!["event"]!.GetValue<string>() == "error")!["example"]!;
            Assert.Equal("Failed", error["status"]!.GetValue<string>());
            var quick = document["paths"]![$"/api/{side}/assistant/quick/{{key}}"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["success"]!["value"]!;
            Assert.Equal(new[] { "user", "assistant" }, quick["messages"]!.AsArray().Select(value => value!["role"]!.GetValue<string>()));
        }
    }

    [Fact]
    public void Creation_and_settings_examples_are_semantically_consistent()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
        foreach (var side in new[] { "me", "organization", "platform" })
        {
            var created = document["paths"]![$"/api/{side}/assistant/conversations"]!["post"]!["responses"]!["201"]!["content"]!["application/json"]!["examples"]!["success"]!["value"]!.AsObject();
            Assert.True(created.ContainsKey("title"));
            Assert.Null(created["title"]);
            Assert.False(created["isSaved"]!.GetValue<bool>());
        }
        foreach (var side in new[] { "organization", "platform" })
            foreach (var method in new[] { "get" })
            {
                var settings = document["paths"]![$"/api/{side}/assistant/settings"]![method]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["success"]!["value"]!;
                var hasKey = settings["hasKey"]!.GetValue<bool>();
                var suffix = settings["maskedKeySuffix"]?.GetValue<string>();
                Assert.True(suffix is null || suffix.Length == 4);
                if (hasKey) Assert.NotNull(suffix);
                else Assert.Null(suffix);
            }
    }

    private static IEnumerable<string> References(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var (key, value) in obj)
                if (key == "$ref") yield return value!.GetValue<string>();
                else foreach (var nested in References(value)) yield return nested;
        else if (node is JsonArray array)
            foreach (var item in array)
                foreach (var nested in References(item)) yield return nested;
    }
}
