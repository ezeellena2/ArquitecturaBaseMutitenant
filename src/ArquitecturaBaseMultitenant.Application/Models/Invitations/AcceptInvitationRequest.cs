namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record AcceptInvitationRequest(string? Token, bool AcceptedTerms)
{
    // Se infiere del dueño verificado antes de la transacción; nunca llega desde HTTP.
    internal bool RequiresRegistration { get; init; }
    public override string ToString() => "AcceptInvitationRequest [redacted]";
}
