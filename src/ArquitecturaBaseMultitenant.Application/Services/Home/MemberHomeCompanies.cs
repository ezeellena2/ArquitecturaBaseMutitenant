using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>
/// «Tus empresas» de Inicio-Miembro: las empresas donde la persona tiene permisos, según
/// <see cref="IPermissionService.GetCompanyPermissionsAsync"/>, con sus roles y los miembros si puede verlos.
/// </summary>
internal sealed class MemberHomeCompanies(ICompanyMembershipReader memberships, IPermissionService permissions)
{
    public async Task<IReadOnlyList<HomeCompanyCard>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cards = new List<HomeCompanyCard>();
        foreach (var company in await memberships.ListActiveForUserAsync(userId, cancellationToken))
        {
            var granted = await permissions.GetCompanyPermissionsAsync(userId, company.CompanyId, cancellationToken);
            if (granted.Count == 0) continue;
            int? members = granted.Contains(Permissions.Company.Members.Read, StringComparer.Ordinal)
                ? (await memberships.GetFilterCountsAsync(company.CompanyId, new ListCompanyMembersRequest(),
                    cancellationToken)).Active
                : null;
            cards.Add(new HomeCompanyCard(company.CompanyId, company.CompanyName,
                [.. company.Roles.Select(role => RoleTexts.Name(role.Name))], members));
        }
        return cards;
    }
}
