using ArquitecturaBaseMultitenant.Application.Configuration.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>Los intentos de guardar de una propuesta: la clave que terminó en 2xx, la última con su estado y si quedó sin confirmar.</summary>
internal sealed record AssistantProposalAttempts(IdempotencyKeyState? Saved, AssistantProposalAttemptKey? Latest, IdempotencyKeyState? LatestState,
    bool Unconfirmed);

/// <summary>Una propuesta como la ve quien la pidió ahora, con la acción y lo que la fuente volvió a resolver.</summary>
internal sealed record AssistantProposalView(AssistantProposalResponse Response, AssistantActionDefinition? Action, AssistantActionReview? Review);

/// <summary>
/// Arma la propuesta como la ve quien la pidió (ADR 0068 §9 y §10). El estado se calcula en cada lectura y nunca se guarda:
/// la fuente vuelve a resolver el destino y cada id en el alcance de solo lectura, con los permisos de ahora y en la empresa
/// de la acción; los intentos se leen del almacén de idempotencia, y «Sin confirmar» es haber pasado la validez de la última
/// clave sin que ninguna terminara en 2xx.
/// </summary>
internal sealed class AssistantProposalBuilder(IAssistantReadOnlyScope readOnly, IIdempotencyStore keys, IAssistantProposalRepository proposals,
    AssistantSnapshotPrivacyGuard privacy, IOptions<IdempotencyOptions> options, TimeProvider clock)
{
    internal async Task<AssistantProposalView> BuildAsync(AssistantProposal proposal, CancellationToken ct)
    {
        var stored = AssistantTurnJson.Restore<AssistantProposalValues>(proposal.Values);
        var outcome = await readOnly.RunAsync<AssistantActionCatalog, AssistantActionReviewOutcome>((catalog, c) =>
            catalog.ReviewAsync(proposal.ActionKey, proposal.TargetId, proposal.BaseVersion, stored.Values, c), ct);
        var attempts = await ReadAttemptsAsync(proposal, outcome.Action, ct);
        // Si una cuenta que nombra la propuesta se dio de baja, ya no se abre (0068 §8).
        var subjectGone = (await privacy.CheckAsync(proposal.OwnerUserId, await proposals.ListSubjectsAsync(proposal, ct), ct)).IsFailure;
        (AssistantProposalState State, AssistantProposalUnavailableReason? Reason, string? Location) shown = proposal.State switch
        {
            AssistantProposalStatus.Applied => (AssistantProposalState.Applied, null, proposal.AppliedLocation),
            AssistantProposalStatus.Replaced => (AssistantProposalState.Replaced, null, null),
            _ when attempts.Saved is { } saved => (AssistantProposalState.Applied, null, saved.ResponseLocation),
            _ when attempts.Unconfirmed => (AssistantProposalState.Unconfirmed, null, null),
            _ when subjectGone => (AssistantProposalState.Unavailable, AssistantProposalUnavailableReason.Subject, null),
            _ when outcome.Reason is { } reason => (AssistantProposalState.Unavailable, reason, null),
            _ => (AssistantProposalState.New, null, null),
        };
        // Lo que muestra la tarjeta lo resuelve la fuente; nunca es texto del modelo. Sin resolver, no se muestra nada.
        var review = subjectGone ? null : outcome.Review;
        var values = review?.Values.Select(value => new AssistantProposalValueResponse(value.Field, value.Value, value.Label, value.Doubt)).ToArray() ?? [];
        var target = review?.Target is { } now ? new AssistantProposalTargetResponse(now.Id, now.Label, now.Version, now.Detail) : null;
        return new(new AssistantProposalResponse(proposal.Id, proposal.ConversationId, AssistantProposalKind.Action, proposal.ActionId,
            proposal.ActionVersion, shown.State, shown.Reason, values, target, proposal.BaseVersion, shown.Location), outcome.Action, review);
    }

    /// <summary>
    /// Lo que el almacén sabe de las claves de la propuesta. Al crear, una clave cuenta como guardada solo si terminó en 2xx
    /// en la ruta que guarda la acción, para esa persona y en esa organización.
    /// </summary>
    internal async Task<AssistantProposalAttempts> ReadAttemptsAsync(AssistantProposal proposal, AssistantActionDefinition? action, CancellationToken ct)
    {
        var list = proposal.ReadAttemptKeys();
        if (list.Count == 0) return new(null, null, null, false);
        IdempotencyKeyState? saved = null, latestState = null;
        for (var index = 0; index < list.Count; index++)
        {
            var state = await keys.FindAsync(proposal.TenantId, proposal.OwnerUserId, list[index].Key, ct);
            if (index == list.Count - 1) latestState = state;
            if (saved is null && state is { ResponseStatusCode: >= 200 and < 300 } && SavedBy(action, state.Route)) saved = state;
        }
        var latest = list[^1];
        var expired = latest.CreatedAtUtc.AddHours(options.Value.ValidityHours) <= clock.GetUtcNow().UtcDateTime;
        return new(saved, latest, latestState, saved is null && expired);
    }

    // La ruta que guarda, como la reserva el filtro de idempotencia: «POST api/roles» es «/api/roles».
    private static bool SavedBy(AssistantActionDefinition? action, string route) => action is not { Kind: AssistantActionKind.Create }
        || string.Equals(route, "/" + action.SaveRoute[(action.SaveRoute.IndexOf(' ', StringComparison.Ordinal) + 1)..], StringComparison.Ordinal);
}
