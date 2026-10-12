using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal sealed record AssistantTurnPreparation(ExecutionActor Actor, string OrganizationTimeZoneId,
    IReadOnlyList<AssistantHistoryMessageRow> History, Guid[] Subjects);
internal sealed class AssistantTurnPreparationBuilder(IExecutionActorFactory actors, ITenantSettingsReader settings,
    IAssistantTurnRepository turns, AssistantSnapshotPrivacyGuard privacy, AssistantConversationPolicy policy)
{
    internal async Task<Result<AssistantTurnPreparation>> BuildAsync(Guid? conversationId, CancellationToken ct)
    {
        var actor = await actors.CaptureAsync(ct);
        if (actor.IsFailure) return actor.Error;
        var zone = actor.Value.TenantId is null ? actor.Value.TimeZoneId : (await settings.FindCurrentAsync(ct))?.DefaultTimeZoneId ?? actor.Value.TimeZoneId;
        IReadOnlyList<AssistantHistoryMessageRow> history = [];
        if (conversationId is { } id)
        {
            var allowed = await policy.AuthorizeAsync(ct);
            if (allowed.IsFailure) return allowed.Error;
            history = await turns.FindHistoryPrivacyAsync(actor.Value.OwnerUserId, actor.Value.Access, id, ct);
            if (!HasKnownProvenance(history)) return AssistantErrors.ConversationNotFound;
        }
        var subjects = history.SelectMany(message => message.SubjectUserIds).Distinct().Order().ToArray();
        var safe = await privacy.CheckAsync(actor.Value.OwnerUserId, subjects, ct);
        return safe.IsFailure ? safe.Error : new AssistantTurnPreparation(actor.Value, zone, history, subjects);
    }
    internal static bool HasKnownProvenance(IReadOnlyList<AssistantHistoryMessageRow> rows) =>
        rows.All(message => message.Role == nameof(AssistantMessageRole.User) || message.PrivacySubjectsKnown);
}
