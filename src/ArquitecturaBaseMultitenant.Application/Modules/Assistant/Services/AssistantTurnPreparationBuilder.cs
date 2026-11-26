using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantTurnPreparation(ExecutionActor Actor, string OrganizationTimeZoneId,
    IReadOnlyList<AssistantHistoryMessageRow> History, Guid[] Subjects, TimeSpan TurnTimeout)
{
    /// <summary>
    /// 0068 §6: las acciones que se le pueden ofrecer a quien pide, leídas antes de la transacción, como dato del turno. Al
    /// aceptarlo, el turno sella las calificadas con su modelo.
    /// </summary>
    public IReadOnlyList<AssistantActionDefinition> Actions { get; init; } = [];
}
internal sealed class AssistantTurnPreparationBuilder(IExecutionActorFactory actors, AssistantQuotaDayResolver days,
    IAssistantTurnRepository turns, AssistantSnapshotPrivacyGuard privacy, AssistantConversationPolicy policy,
    AssistantActionCatalog actions, IOptions<AssistantOptions> options)
{
    internal async Task<Result<AssistantTurnPreparation>> BuildAsync(Guid? conversationId, CancellationToken ct)
    {
        var actor = await actors.CaptureAsync(ct);
        if (actor.IsFailure) return actor.Error;
        // 271: la zona de la organización corta el día y el mes; fuera de una, la del perfil. La hora del pedido que lee el
        // proveedor va con la zona del perfil (0064 §9), que el turno lleva en su actor.
        var zone = await days.ZoneAsync(actor.Value.Access, actor.Value.TimeZoneId, ct);
        IReadOnlyList<AssistantHistoryMessageRow> history = [];
        if (conversationId is { } id)
        {
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            history = await turns.FindHistoryPrivacyAsync(actor.Value.OwnerUserId, actor.Value.Access, id, ct);
            // 0064 §6: lo que esta versión no sabe leer no recibe un turno, como su GET, antes de reservar el cupo.
            if (!IsReadable(history)) return AssistantErrors.ConversationUnavailable;
            if (!HasKnownProvenance(history)) return AssistantErrors.ConversationNotFound;
        }
        var subjects = history.SelectMany(message => message.SubjectUserIds).Distinct().Order().ToArray();
        var safe = await privacy.CheckAsync(actor.Value.OwnerUserId, subjects, ct);
        if (safe.IsFailure) return safe.Error;
        // Solo un turno nuevo (con su conversación) ofrece acciones; detener o subir un adjunto no las lee.
        IReadOnlyList<AssistantActionDefinition> offered = conversationId is null ? [] : await actions.ListAsync(null, ct);
        return new AssistantTurnPreparation(actor.Value, zone, history, subjects, options.Value.TurnTimeout) { Actions = offered };
    }
    /// <summary>0068 §6: lo que sella el turno al aceptarlo, de lo preparado, solo lo calificado con su modelo.</summary>
    internal IReadOnlyList<string> Seal(AssistantTurnPreparation prepared, string providerModelId) =>
        actions.Seal(prepared.Actions, providerModelId);
    internal static bool IsReadable(IReadOnlyList<AssistantHistoryMessageRow> rows) => rows.All(message => message.Readable);
    internal static bool HasKnownProvenance(IReadOnlyList<AssistantHistoryMessageRow> rows) =>
        rows.All(message => message.Role == nameof(AssistantMessageRole.User) || message.PrivacySubjectsKnown);
}
