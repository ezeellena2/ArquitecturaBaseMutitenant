using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Capacidad nominal del OTP: sólo las reservas exactas de ese correo pueden cambiar y auditarse.</summary>
internal sealed class LoginInvitationScope
{
    private IReadOnlyList<LoginInvitationRow> _rows = [];
    internal void Authorize(IReadOnlyList<LoginInvitationRow> rows) => _rows = rows;
    internal bool Allows(object entity) => entity switch
    {
        Invitation invitation => _rows.Any(row => row.TenantId == invitation.TenantId && row.InvitationId == invitation.Id),
        Member member => _rows.Any(row => row.TenantId == member.TenantId && row.MemberId == member.Id),
        _ => false,
    };
}
