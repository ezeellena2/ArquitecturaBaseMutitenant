using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// La búsqueda de personas que comparten Usuarios y Miembros de empresa: el nombre, cualquier correo verificado y el
/// destino de una invitación pendiente. Cada parte busca con <c>ApplySearch</c>, sin acentos ni mayúsculas y con los
/// comodines escapados.
/// </summary>
internal static class MemberSearch
{
    public static IQueryable<Guid> MatchingMemberIds(ApplicationDbContext context, Guid tenantId, string search)
    {
        var byName = context.Users.AsNoTracking()
            .ApplySearch(search, user => user.DisplayName)
            .Select(user => (Guid?)user.Id);
        var byEmail = context.LoginMethods.AsNoTracking()
            .Where(method => method.Type == LoginMethodType.Email && method.VerifiedAtUtc != null)
            .ApplySearch(search, method => method.Value)
            .Select(method => (Guid?)method.UserId);
        // El destino es un Email con conversor: se busca sobre la columna, que guarda el valor canónico.
        var byInvitation = context.Invitations.AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId && invitation.Status == InvitationStatus.Pending)
            .ApplySearch(search, invitation => EF.Property<string>(invitation, nameof(Invitation.Destination)))
            .Select(invitation => invitation.MemberId);
        return context.Members.AsNoTracking()
            .Where(member => member.TenantId == tenantId && (byName.Contains(member.UserId)
                || byEmail.Contains(member.UserId) || byInvitation.Contains(member.Id)))
            .Select(member => member.Id);
    }
}
