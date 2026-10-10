using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

public interface IAssistantTool
{
    string Key { get; }
    Access? Access { get; }
    IReadOnlyList<string> Permissions { get; }
    string DescriptionKey { get; }
    JsonElement Schema { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    Task<Result<AssistantToolData>> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
