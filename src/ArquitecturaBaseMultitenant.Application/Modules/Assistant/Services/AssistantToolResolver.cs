using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantToolResolver(AssistantToolCatalog catalog)
{
    internal Task<bool> IsAvailableAsync(string key, CancellationToken ct) =>
        catalog.Find(key)?.IsAvailableAsync(ct) ?? Task.FromResult(false);
    internal Task<Result<AssistantToolData>> ExecuteAsync(string key, JsonElement arguments, CancellationToken ct) =>
        catalog.Find(key)?.ExecuteAsync(arguments, ct) ?? Task.FromResult(Result.Failure<AssistantToolData>(AssistantErrors.QuickQueryNotAvailable));
}
