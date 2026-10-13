using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AssistantQualityContractTests
{
    [Fact]
    public void The_production_provider_has_no_test_double_and_respects_the_dependency_limit()
    {
        var infrastructure = System.Reflection.Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure");
        Assert.DoesNotContain(infrastructure.GetTypes(), type => type.Name is "FakeChatClient" or "AssistantFakeChatMetrics");
        var factory = infrastructure.GetType("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AnthropicChatClientFactory", throwOnError: true)!;
        Assert.All(factory.GetConstructors(), constructor => Assert.InRange(constructor.GetParameters().Length, 0, 8));
    }

    [Fact]
    public void Implemented_routes_publish_the_shared_limits_and_no_placeholder_status()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs/contracts/openapi.json")))!;
        foreach (var path in document["paths"]!.AsObject().Where(path => path.Key.Contains("/assistant", StringComparison.Ordinal)))
            foreach (var action in path.Value!.AsObject().Where(action => action.Key is "get" or "post" or "put" or "delete"))
                Assert.Null(action.Value!["responses"]!["501"]);
        var schemas = document["components"]!["schemas"]!;
        Assert.Equal(TextLimits.LongText, schemas["UpdatePlatformAssistantSettingsHttpRequest"]!["properties"]!["apiKey"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(TextLimits.Description, schemas["CreateAssistantModelHttpRequest"]!["properties"]!["name"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(AssistantTechnicalLimits.MaxAttachments, schemas["SendAssistantMessageHttpRequest"]!["properties"]!["attachmentIds"]!["maxItems"]!.GetValue<int>());
        Assert.Equal(AssistantTechnicalLimits.AttachmentMediaTypes,
            schemas["AssistantAttachmentSummaryResponse"]!["properties"]!["mediaType"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()));
    }
}
