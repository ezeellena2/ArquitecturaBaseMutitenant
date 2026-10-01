namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>El token llega en el cuerpo o en una continuación HttpOnly del mismo navegador.</summary>
public sealed record PreviewInvitationRequest(string? Token)
{
    public override string ToString() => "PreviewInvitationRequest [redacted]";
}
