using System.Globalization;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.AI;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed class AssistantTurnToolBuilder(AssistantToolCatalog catalog, AssistantActionTools actions, AssistantConversationPolicy policy,
    IExecutionActorContext actors)
{
    /// <summary>Las herramientas del turno; con acciones selladas que se siguen ofreciendo, también las tres de las acciones (0068 §7).</summary>
    internal async Task<IReadOnlyList<AITool>> BuildAsync(bool offersActions, CancellationToken ct)
    {
        var tools = (await catalog.ListAsync(ct)).Select(tool => Declare(tool.Key, tool.DescriptionKey, tool.Schema)).ToList();
        if (offersActions) tools.AddRange(AssistantActionTools.Keys.Select(key => Declare(key, "Tool." + key, AssistantActionTools.Schema(key))));
        return tools;
    }
    /// <summary>La lista precargada del turno, como dato; null si de lo sellado ya no se ofrece nada.</summary>
    internal async Task<AssistantToolExecution?> PreloadActionsAsync(AssistantActionTurn turn, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return null;
        var list = await actions.ListAsync(turn, Culture(), ct);
        return list.Actions is { Count: 0 } ? null : new AssistantToolExecution(Serialize(list), []);
    }
    internal string Label(string key) => AssistantActionTools.Handles(key) ? "Tool." + key
        : catalog.Find(key)?.DescriptionKey ?? throw new InvalidOperationException("The tool is not in the server catalog.");
    internal async Task<Result<AssistantToolExecution>> ExecuteAsync(string key, JsonElement arguments, AssistantActionTurn? turn, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (AssistantActionTools.Handles(key))
        {
            if (turn is null) return AssistantErrors.QuickQueryNotAvailable;
            // ADR 0068 §7: lo rechazado vuelve al modelo como dato, con la ruta de cada campo; no es un error que corte el turno.
            AssistantActionToolData result = key switch
            {
                AssistantActionTools.ListKey => await actions.ListAsync(turn, Culture(), ct),
                AssistantActionTools.ReadKey => await actions.ReadAsync(arguments, turn, Culture(), ct),
                _ => await actions.ProposeAsync(arguments, turn, ct),
            };
            allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            return Execution(result);
        }
        var tool = catalog.Find(key);
        if (tool is null || !await tool.IsAvailableAsync(ct)) return AssistantErrors.QuickQueryNotAvailable;
        var data = await tool.ExecuteAsync(arguments, ct);
        if (data.IsFailure) return data.Error;
        allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        if (!await tool.IsAvailableAsync(ct)) return AssistantErrors.QuickQueryNotAvailable;
        return Execution(data.Value);
    }

    /// <summary>
    /// Las personas que copia una herramienta al turno, por su proyección cerrada; null si la proyección no es conocida, y
    /// entonces el turno falla cerrado. Tool output is data only: it is never turned into a system/developer message.
    /// </summary>
    internal static Guid[]? SubjectsOf(AssistantToolData data) => data switch
    {
        // Quick's typed projection protocol carries account copies explicitly, including a proven empty set.
        AssistantQuickToolData quick => quick.SubjectCopies.Select(copy => copy.AccountId).Distinct().ToArray(),
        AssistantPendingToolData { PrivacySubjectsKnown: true } pending => pending.SubjectUserIds.Distinct().ToArray(),
        // Closed projections of organization/role names and authored help; no account-name copies.
        AssistantRolesToolData or AssistantCompaniesToolData or AssistantHelpToolData => [],
        // 0068 §7 y §8: al proveedor van tipos de acción, campos y nombres de definiciones, nunca registros ni personas. Las
        // personas que nombra una propuesta aceptada (el destino y los Record de una persona) quedan como sujetos de su mensaje.
        AssistantActionToolData action => action.Resolution?.SubjectUserIds.Distinct().ToArray() ?? [],
        _ => null,
    };

    private static Result<AssistantToolExecution> Execution(AssistantToolData data) => SubjectsOf(data) is { } subjects
        ? new AssistantToolExecution(Serialize(data), subjects) { Action = data as AssistantActionToolData }
        : AssistantErrors.QuickQueryNotAvailable;
    private static string Serialize(AssistantToolData data) => JsonSerializer.Serialize(data, data.GetType(), AssistantTurnJson.Options);
    private CultureInfo Culture() => CultureInfo.GetCultureInfo(actors.Actor?.Culture ?? throw new InvalidOperationException("A worker actor is required."));
    private AIFunctionDeclaration Declare(string key, string descriptionKey, JsonElement schema) => AIFunctionFactory.CreateDeclaration(key,
        Resources.AssistantToolTexts.ResourceManager.GetString(descriptionKey, Culture())
            ?? throw new InvalidOperationException("The tool resource is missing."), schema);
}
