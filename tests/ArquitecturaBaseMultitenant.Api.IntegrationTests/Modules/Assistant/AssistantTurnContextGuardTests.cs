using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantTurnContextGuardTests
{
    private static AssistantChatBinding Binding => new(AssistantMode.PlatformKey, Guid.NewGuid(), "fixture-model", AssistantEffort.Low);

    [Theory]
    [InlineData("arguments")]
    [InlineData("callId")]
    [InlineData("thinking")]
    [InlineData("protected")]
    [InlineData("result")]
    [InlineData("description")]
    public void Content_outside_plain_text_cannot_escape_the_complete_input_bound(string field)
    {
        var large = new string('x', 60000);
        AIContent content = field switch
        {
            "arguments" => new FunctionCallContent("call", "my_account", new Dictionary<string, object?> { ["padding"] = large }),
            "callId" => new FunctionCallContent(large, "my_account", new Dictionary<string, object?>()),
            "thinking" => new TextReasoningContent(large),
            "protected" => new TextReasoningContent("") { ProtectedData = large },
            "result" => new FunctionResultContent("call", large),
            _ => new TextContent("Brief"),
        };
        var messages = new[] { new ChatMessage(ChatRole.Assistant, [content]) };
        var tools = new AITool[] { Declaration(field == "description" ? large : "Description") };
        Assert.False(AssistantTurnContextGuard.Fits(messages, tools, Binding));
    }

    [Fact]
    public void Small_complete_context_with_opaque_blocks_and_a_tool_is_accepted()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Brief"),
            new ChatMessage(ChatRole.Assistant,
                [new TextReasoningContent("Private fixture") { ProtectedData = "opaque-fixture" },
                 new FunctionCallContent("call", "my_account", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call", "{}")]),
        };
        Assert.True(AssistantTurnContextGuard.Fits(messages, [Declaration("Description")], Binding));
    }

    private static AIFunctionDeclaration Declaration(string description) => AIFunctionFactory.CreateDeclaration("my_account", description,
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, additionalProperties = false }));

    [Fact]
    public void An_inline_image_uses_the_provider_visual_bound_instead_of_base64_tokens()
    {
        var messages = new[] { new ChatMessage(ChatRole.User, [new TextContent("Consulta"), new DataContent(new byte[100_000], "image/png")]) };
        Assert.True(AssistantTurnContextGuard.Fits(messages, [], Binding));
        var unknown = new[] { new ChatMessage(ChatRole.User, [new DataContent(new byte[100], "application/octet-stream")]) };
        Assert.False(AssistantTurnContextGuard.Fits(unknown, [], Binding));
    }
}
