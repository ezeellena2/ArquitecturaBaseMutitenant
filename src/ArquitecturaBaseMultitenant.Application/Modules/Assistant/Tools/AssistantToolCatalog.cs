using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

/// <summary>Discovery is filtered; it never replaces execution authorization.</summary>
internal sealed class AssistantToolCatalog(IEnumerable<IAssistantTool> tools, AssistantAccessPolicy access)
{
    private readonly IReadOnlyList<IAssistantTool> _tools = tools.ToArray();
    internal IAssistantTool? Find(string key) => _tools.SingleOrDefault(tool => tool.Key == key);
    internal async Task<IReadOnlyList<IAssistantTool>> ListAsync(CancellationToken ct)
    {
        var available = new List<IAssistantTool>();
        foreach (var tool in _tools)
            if ((tool.Access is null || tool.Access == access.CurrentAccess) && await tool.IsAvailableAsync(ct)) available.Add(tool);
        return available;
    }
}
