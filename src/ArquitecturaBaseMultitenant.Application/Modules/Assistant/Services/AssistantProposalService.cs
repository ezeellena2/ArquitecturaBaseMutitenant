using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Las tres rutas de la propuesta (ADR 0068 §9 y §10), solo de quien la pidió: la de otra persona u organización es 404. El
/// estado se calcula afuera de la transacción, con la fuente en el alcance de solo lectura; adentro se bloquea la propuesta, se
/// vuelve a mirar su estado guardado y sus claves, y se escribe.
/// </summary>
internal sealed class AssistantProposalService(IAssistantProposalRepository proposals, AssistantProposalBuilder builder, AssistantProposalWriter writer,
    AssistantConversationPolicy policy, ICurrentUser user, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<AssistantProposalService> logger)
    : IAssistantProposalService
{
    public Task<Result<AssistantProposalResponse>> GetAsync(Guid proposalId, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantProposalResponse>(logger, clock, "AssistantProposalGet", async () =>
        {
            var view = await ReadAsync(proposalId, ct);
            return view.IsFailure ? view.Error : view.Value.Response;
        });

    public Task<Result<AssistantProposalAttemptResponse>> AttemptAsync(Guid proposalId, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantProposalAttemptResponse>(logger, clock, "AssistantProposalAttempt", async () =>
        {
            var view = await ReadAsync(proposalId, ct);
            if (view.IsFailure) return view.Error;
            var shown = view.Value.Response;
            // Aplicada por una clave que el servidor todavía no marcó: la marca adentro y responde AlreadyApplied.
            if (shown.State is not (AssistantProposalState.New or AssistantProposalState.Applied)) return Unavailable(shown);
            return await unitOfWork.ExecuteInTransactionAsync(c => writer.AttemptAsync(proposalId, view.Value.Action, c), CommitPolicy.OnAnyResult, ct);
        });

    public Task<Result<AssistantProposalResponse>> AppliedAsync(Guid proposalId, AssistantProposalAppliedRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<AssistantProposalResponse>(logger, clock, "AssistantProposalApplied", async () =>
        {
            var view = await ReadAsync(proposalId, ct);
            if (view.IsFailure) return view.Error;
            var shown = view.Value.Response;
            // Al quitar o dar de baja, que el destino ya no se resuelva es la prueba de que se guardó.
            var removed = view.Value.Action?.Kind == ArquitecturaBaseMultitenant.Application.Models.AssistantActions.AssistantActionKind.Remove
                && shown.UnavailableReason == AssistantProposalUnavailableReason.Target;
            if (shown.State is not (AssistantProposalState.New or AssistantProposalState.Applied) && !removed) return Unavailable(shown);
            var applied = await unitOfWork.ExecuteInTransactionAsync(c => writer.ApplyAsync(proposalId, request, view.Value, c), CommitPolicy.OnSuccess, ct);
            if (applied.IsFailure) return applied.Error;
            var after = await ReadAsync(proposalId, ct);
            return after.IsFailure ? after.Error : after.Value.Response;
        });

    private async Task<Result<AssistantProposalView>> ReadAsync(Guid proposalId, CancellationToken ct)
    {
        var allowed = await policy.AuthorizeAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var proposal = await proposals.FindAsync(user.UserId ?? throw new InvalidOperationException("A session is required."),
            user.Access ?? throw new InvalidOperationException("An access is required."), proposalId, ct);
        if (proposal is null) return AssistantErrors.ProposalNotFound;
        return await builder.BuildAsync(proposal, ct);
    }

    private static Error Unavailable(AssistantProposalResponse shown) => AssistantErrors.ProposalUnavailable(shown.State switch
    {
        AssistantProposalState.Replaced => "replaced",
        AssistantProposalState.Unconfirmed => "unconfirmed",
        _ => "unavailable",
    }, shown.UnavailableReason switch
    {
        AssistantProposalUnavailableReason.Permission => "permission",
        AssistantProposalUnavailableReason.Module => "module",
        AssistantProposalUnavailableReason.Version => "version",
        AssistantProposalUnavailableReason.Target => "target",
        AssistantProposalUnavailableReason.Subject => "subject",
        _ => null,
    });
}
