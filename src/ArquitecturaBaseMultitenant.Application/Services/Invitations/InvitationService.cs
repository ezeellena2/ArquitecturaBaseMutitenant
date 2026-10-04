using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// Muestra y acepta una invitación. Entra a la organización del token solo después de probarlo, y lee la invitación
/// adentro, antes de abrir la transacción.
/// </summary>
internal sealed class InvitationService(InvitationPreviewResolver previews, InvitationAcceptanceResolver resolver,
    InvitationAcceptanceWriter acceptor, ITenantScope tenantScope, IRequestValidator validator, IUnitOfWork unitOfWork,
    InvitationSessionRestorer sessions, TimeProvider timeProvider,
    ILogger<InvitationService> logger) : IInvitationService
{
    public Task<Result<InvitationPreviewResponse>> PreviewAsync(PreviewInvitationRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<InvitationPreviewResponse>(logger, timeProvider, "PreviewInvitation", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var proven = previews.ReadToken(request);
            if (proven is null) return new InvitationPreviewResponse(InvitationPreviewState.Invalid);
            using var scope = tenantScope.Enter(proven.Proof.TenantId);
            return await previews.PreviewAsync(proven, ct);
        });

    public Task<Result<AcceptInvitationResponse>> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<AcceptInvitationResponse>(logger, timeProvider, "AcceptInvitation", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var proven = resolver.ReadToken(request);
            if (proven is null) return InvitationErrors.Invalid;
            using var scope = tenantScope.Enter(proven.Proof.TenantId);
            var prepared = await resolver.PrepareAsync(request, proven, ct);
            if (prepared.IsFailure) return prepared.Error;
            var accepted = await unitOfWork.ExecuteInTransactionAsync(token => acceptor.AcceptAsync(prepared.Value, token),
                CommitPolicy.OnSuccess, ct);
            if (accepted.IsFailure) return accepted.Error;
            return await sessions.CompleteAsync(prepared.Value, accepted.Value, ct);
        });
}
