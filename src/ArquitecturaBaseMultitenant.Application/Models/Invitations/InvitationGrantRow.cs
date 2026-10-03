namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

/// <summary>Lo que ofrece una invitación: los roles de quien invita y las empresas y roles de la persona invitada.</summary>
/// <remarks>Los nombres de rol llegan como se guardaron; el servicio traduce los de sistema con <c>RoleTexts</c>.</remarks>
public sealed record InvitationGrantRow(IReadOnlyList<string> InviterRoles, IReadOnlyList<string> Companies,
    IReadOnlyList<string> Roles)
{
    public static InvitationGrantRow Empty { get; } = new([], [], []);
}
