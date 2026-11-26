using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantToolResolver(AssistantToolCatalog catalog, IAssistantReadOnlyScope readOnly)
{
    internal Task<bool> IsAvailableAsync(string key, CancellationToken ct) =>
        catalog.Find(key)?.IsAvailableAsync(ct) ?? Task.FromResult(false);
    /// <summary>ADR 0068 §5: la herramienta corre en el alcance READ ONLY y descartado, con la organización y la persona de quien pide.</summary>
    internal Task<Result<AssistantToolData>> ExecuteAsync(string key, JsonElement arguments, CancellationToken ct) =>
        readOnly.RunAsync<AssistantToolCatalog, Result<AssistantToolData>>((inner, c) =>
            inner.Find(key)?.ExecuteAsync(arguments, c) ?? Task.FromResult(Result.Failure<AssistantToolData>(AssistantErrors.QuickQueryNotAvailable)), ct);
}
