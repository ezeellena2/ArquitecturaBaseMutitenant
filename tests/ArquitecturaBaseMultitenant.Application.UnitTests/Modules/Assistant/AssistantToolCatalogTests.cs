using ArquitecturaBaseMultitenant.Application.Modules.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantToolCatalogTests
{
    [Fact]
    public void Read_only_tool_catalog_is_available_for_direct_execution()
    {
        var assembly = typeof(AssistantModule).Assembly;
        Assert.NotNull(assembly.GetType("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.IAssistantTool"));
        Assert.NotNull(assembly.GetType("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.AssistantToolCatalog"));
    }

    [Fact]
    public void Catalog_has_exactly_nineteen_closed_read_tools_with_server_owned_identity()
    {
        var types = typeof(IAssistantTool).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(IAssistantTool))).ToArray();
        var tools = types.Select(type => (IAssistantTool)Activator.CreateInstance(type,
            type.GetConstructors().Single().GetParameters().Select(_ => (object?)null).ToArray())!).ToArray();
        Assert.Equal(19, tools.Length);
        Assert.Equal(9, tools.Count(tool => tool.Access == Access.Business));
        Assert.Equal(6, tools.Count(tool => tool.Access == Access.Platform));
        Assert.Equal(3, tools.Count(tool => tool.Access == Access.Consumer));
        Assert.Single(tools, tool => tool.Key == "help_article" && tool.Access is null);
        Assert.Equal(19, tools.Select(tool => tool.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(tools, tool =>
        {
            Assert.False(tool.Schema.GetProperty("additionalProperties").GetBoolean());
            var fields = tool.Schema.GetProperty("properties");
            Assert.False(fields.TryGetProperty("userId", out _));
            Assert.False(fields.TryGetProperty("tenantId", out _));
            Assert.False(fields.TryGetProperty("url", out _));
            Assert.False(fields.TryGetProperty("sql", out _));
            if (fields.TryGetProperty("pageSize", out var size)) Assert.Equal(20, size.GetProperty("maximum").GetInt32());
        });
        Assert.Equal(new[] { "roles.read", "users.read" }, tools.Single(tool => tool.Key == "org_role_members").Permissions);
    }
}
