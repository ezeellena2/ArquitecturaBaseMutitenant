using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Qué invitación se resuelve desde la ficha de una empresa. La incluye si su persona tiene una membresía activa en
/// ella (decisión 97); es únicamente de ella si, además, no da roles de la organización ni entra a otra empresa (otra
/// membresía activa). Revocar cancela la invitación entera, así que sin <c>users.invite</c> se resuelven solo las que
/// son únicamente de esa empresa (decisión 100). <c>CompanyMembershipReader</c> aplica la misma regla en SQL a la fila
/// de Miembros.
/// </summary>
internal static class CompanyInvitationPolicy
{
    public static bool CanResolve(CompanyInvitationScope company, bool givesOrganizationRoles,
        IReadOnlyCollection<(Guid CompanyId, CompanyMembershipStatus Status)> memberships)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(memberships);
        var includes = memberships.Any(membership => membership.CompanyId == company.CompanyId
            && membership.Status == CompanyMembershipStatus.Active);
        var onlyThisCompany = !givesOrganizationRoles && memberships.All(membership =>
            membership.CompanyId == company.CompanyId || membership.Status != CompanyMembershipStatus.Active);
        return includes && (company.CanInvite || onlyThisCompany);
    }
}

/// <summary>
/// Desde qué empresa se reenvía o revoca una invitación, y si quien lo hace tiene <c>users.invite</c>. Desde Usuarios
/// no hay empresa.
/// </summary>
internal sealed record CompanyInvitationScope(Guid CompanyId, bool CanInvite);
