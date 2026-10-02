using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

internal sealed record IssuedInvitation(Guid InvitationId, Member Member)
{
    public Guid MemberId => Member.Id;
}
