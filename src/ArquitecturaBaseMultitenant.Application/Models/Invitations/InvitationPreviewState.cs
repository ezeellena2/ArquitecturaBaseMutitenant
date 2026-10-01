namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public enum InvitationPreviewState
{
    NoAccount,
    SignInRequired,
    Ready,
    WrongSession,
    Expired,
    Invalid,
    OrganizationSuspended,
    Accepted,
}
