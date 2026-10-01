using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationService(InvitationPreviewFlow previews, InvitationAcceptanceResolver resolver,
    InvitationAcceptor acceptor, IRequestValidator validator, IUnitOfWork unitOfWork,
    InvitationSessionRestorer sessions, TimeProvider timeProvider,
    ILogger<InvitationService> logger) : IInvitationService
{
    public Task<Result<InvitationPreviewResponse>> PreviewAsync(PreviewInvitationRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<InvitationPreviewResponse>(logger, timeProvider, "PreviewInvitation", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            return await previews.PreviewAsync(request, ct);
        });

    public Task<Result<AcceptInvitationResponse>> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<AcceptInvitationResponse>(logger, timeProvider, "AcceptInvitation", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var prepared = await resolver.PrepareAsync(request, ct);
            if (prepared.IsFailure) return prepared.Error;
            using var scope = resolver.Enter(prepared.Value);
            var accepted = await unitOfWork.ExecuteInTransactionAsync(token => acceptor.AcceptAsync(prepared.Value, token),
                CommitPolicy.OnSuccess, ct);
            if (accepted.IsFailure) return accepted.Error;
            return await sessions.CompleteAsync(prepared.Value, accepted.Value, ct);
        });
}
