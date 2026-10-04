namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>
/// El token del pedido o de la continuación, ya probado, con la continuación que trajo el navegador. Con él,
/// <c>InvitationService</c> entra a la organización de la invitación.
/// </summary>
internal sealed record ProvenInvitationToken(string Token, InvitationTokenData Proof,
    InvitationContinuation? Continuation)
{
    public override string ToString() => "ProvenInvitationToken [redacted]";
}
