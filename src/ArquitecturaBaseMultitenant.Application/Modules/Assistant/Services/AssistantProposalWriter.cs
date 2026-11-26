using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Escribe la propuesta adentro de la transacción del llamador (ADR 0068 §8 y §10): la guarda con el mensaje del turno y
/// reemplaza la nueva anterior de su clase; da la clave de cada intento de guardar y la marca aplicada cuando el servidor lo
/// comprueba.
/// </summary>
internal sealed class AssistantProposalWriter(IAssistantProposalRepository proposals, AssistantProposalBuilder builder, ICurrentUser user,
    TimeProvider clock)
{
    private Guid Owner => user.UserId ?? throw new InvalidOperationException("A session is required.");
    private Domain.Users.Access Access => user.Access ?? throw new InvalidOperationException("An access is required.");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>La propuesta aceptada del turno, con su mensaje: una por turno, y la nueva anterior de su clase queda reemplazada.</summary>
    internal async Task<Result> SaveAsync(IAssistantTurn turn, AssistantActionToolData accepted, CancellationToken ct)
    {
        if (accepted is not { Outcome: AssistantActionToolData.Accepted, ProposalId: { } id, Resolution: { } resolution })
            throw new InvalidOperationException("Only an accepted proposal is saved.");
        if (await proposals.ExistsForMessageAsync(turn.ConversationId, turn.AssistantMessageId, ct)) return AssistantErrors.ActionOnePerTurn;
        // La nueva anterior se toma con lock y se lee después: un intento de guardarla que la tiene la deja con su clave.
        foreach (var previous in await proposals.LockNewAsync(turn.ConversationId, AssistantProposal.ActionKind, ct)) previous.Replace();
        var values = AssistantTurnJson.Serialize(new AssistantProposalValues(accepted.WrittenTarget, resolution.Values));
        proposals.Add(AssistantProposal.Create(id, turn.ConversationId, turn.AssistantMessageId, Owner, resolution.Action.Id,
            resolution.Action.Version, values, resolution.TargetId, resolution.BaseVersion, Now));
        return Result.Success();
    }

    /// <summary>
    /// La clave del intento de guardar: la misma mientras ese intento no termine; una nueva después de un 4xx. Si una clave ya
    /// terminó en 2xx, la marca aplicada y responde <c>AlreadyApplied</c> (la transacción se confirma igual).
    /// </summary>
    internal async Task<Result<AssistantProposalAttemptResponse>> AttemptAsync(Guid id, AssistantActionDefinition? action, CancellationToken ct)
    {
        var proposal = await proposals.LockAsync(Owner, Access, id, ct);
        if (proposal is null) return AssistantErrors.ProposalNotFound;
        if (Closed(proposal) is { } closed) return closed;
        var attempts = await builder.ReadAttemptsAsync(proposal, action, ct);
        if (attempts.Saved is { } saved)
        {
            proposal.MarkApplied(saved.ResponseLocation, Now);
            return AssistantErrors.ProposalAlreadyApplied(saved.ResponseLocation);
        }
        if (attempts.Unconfirmed) return AssistantErrors.ProposalUnavailable("unconfirmed", null);
        // En curso o sin usar todavía: dos pestañas usan la misma. Terminada sin guardar, va otra.
        if (attempts.Latest is { } latest && attempts.LatestState?.ResponseStatusCode is null)
            return new AssistantProposalAttemptResponse(latest.Key.ToString("D"));
        var key = Guid.NewGuid();
        proposal.AddAttemptKey(key, Now);
        return new AssistantProposalAttemptResponse(key.ToString("D"));
    }

    /// <summary>
    /// La pantalla guardó: al crear, una clave terminó en 2xx en su ruta; al quitar o dar de baja, el destino ya no se resuelve;
    /// al editar, la versión de ahora es la del cuerpo y distinta de la de la propuesta. Sin poder comprobarlo, <c>NotApplied</c>.
    /// </summary>
    internal async Task<Result> ApplyAsync(Guid id, AssistantProposalAppliedRequest request, AssistantProposalView view, CancellationToken ct)
    {
        var proposal = await proposals.LockAsync(Owner, Access, id, ct);
        if (proposal is null) return AssistantErrors.ProposalNotFound;
        if (proposal.State == AssistantProposalStatus.Applied) return Result.Success();
        if (Closed(proposal) is { } closed) return closed;
        switch (view.Action?.Kind)
        {
            case AssistantActionKind.Create:
                if ((await builder.ReadAttemptsAsync(proposal, view.Action, ct)).Saved is not { } saved) return AssistantErrors.ProposalNotApplied;
                proposal.MarkApplied(saved.ResponseLocation, Now);
                return Result.Success();
            case AssistantActionKind.Remove:
                if (view.Response.UnavailableReason != AssistantProposalUnavailableReason.Target) return AssistantErrors.ProposalNotApplied;
                proposal.MarkApplied(null, Now);
                return Result.Success();
            case AssistantActionKind.Edit or AssistantActionKind.Other:
                if (request.Version is not { } version || view.Review?.Target?.Version != version || version == proposal.BaseVersion)
                    return AssistantErrors.ProposalNotApplied;
                proposal.MarkApplied(null, Now);
                return Result.Success();
            default:
                return AssistantErrors.ProposalUnavailable("unavailable", "version");
        }
    }

    private static Error? Closed(AssistantProposal proposal) => proposal.State switch
    {
        AssistantProposalStatus.Applied => AssistantErrors.ProposalAlreadyApplied(proposal.AppliedLocation),
        AssistantProposalStatus.Replaced => AssistantErrors.ProposalUnavailable("replaced", null),
        _ => null,
    };
}
