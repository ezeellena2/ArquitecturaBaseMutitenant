using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IInvitationService
{
    Task<Result<InvitationPreviewResponse>> PreviewAsync(PreviewInvitationRequest request, CancellationToken cancellationToken);
    Task<Result<AcceptInvitationResponse>> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);
}
