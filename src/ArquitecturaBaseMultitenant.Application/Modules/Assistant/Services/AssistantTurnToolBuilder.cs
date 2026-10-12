using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.AI;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnToolBuilder(AssistantToolCatalog catalog, AssistantConversationPolicy policy, IExecutionActorContext actors)
{
    internal async Task<IReadOnlyList<AITool>> BuildAsync(CancellationToken ct) =>
        (await catalog.ListAsync(ct)).Select(tool => (AITool)AIFunctionFactory.CreateDeclaration(tool.Key,
            Resources.AssistantToolTexts.ResourceManager.GetString(tool.DescriptionKey,
                System.Globalization.CultureInfo.GetCultureInfo(actors.Actor?.Culture ?? throw new InvalidOperationException("A worker actor is required.")))
                ?? throw new InvalidOperationException("The tool resource is missing."), tool.Schema)).ToArray();
    internal string Label(string key) => catalog.Find(key)?.DescriptionKey ?? throw new InvalidOperationException("The tool is not in the server catalog.");
    internal async Task<Result<AssistantToolExecution>> ExecuteAsync(string key, JsonElement arguments, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var tool = catalog.Find(key);
        if (tool is null || !await tool.IsAvailableAsync(ct)) return AssistantErrors.QuickQueryNotAvailable;
        var data = await tool.ExecuteAsync(arguments, ct);
        if (data.IsFailure) return data.Error;
        allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (!await tool.IsAvailableAsync(ct)) return AssistantErrors.QuickQueryNotAvailable;
        // Tool output is data only. It is never turned into a system/developer message.
        Guid[] subjects;
        switch (data.Value)
        {
            case AssistantQuickToolData quick:
                // Quick's typed projection protocol carries account copies explicitly, including a proven empty set.
                subjects = quick.SubjectCopies.Select(copy => copy.AccountId).Distinct().ToArray();
                break;
            case AssistantPendingToolData pending when pending.PrivacySubjectsKnown:
                subjects = pending.SubjectUserIds.Distinct().ToArray();
                break;
            case AssistantRolesToolData or AssistantCompaniesToolData or AssistantHelpToolData:
                // Closed projections of organization/role names and authored help; no account-name copies.
                subjects = [];
                break;
            default:
                return AssistantErrors.QuickQueryNotAvailable;
        }
        return new AssistantToolExecution(JsonSerializer.Serialize(data.Value, data.Value.GetType(), AssistantTurnJson.Options), subjects);
    }
}
