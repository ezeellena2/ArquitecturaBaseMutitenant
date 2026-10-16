using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationSessionRestorer(IInvitationFlowContext flow)
{
    internal async Task<AcceptInvitationResponse> CompleteAsync(InvitationAcceptanceContext context,
        InvitationAcceptanceOutcome accepted, CancellationToken ct)
    {
        flow.Remember(new InvitationContinuation(context.Token, context.Nonce, accepted.ExpiresAtUtc.AddMinutes(5)));
        if (accepted.CreatedAccount) await flow.RestoreSessionAsync(accepted.UserId, ct);
        return new AcceptInvitationResponse(accepted.OrganizationId, accepted.CreatedAccount, accepted.NeedsPersonalLoginMethod);
    }
}
