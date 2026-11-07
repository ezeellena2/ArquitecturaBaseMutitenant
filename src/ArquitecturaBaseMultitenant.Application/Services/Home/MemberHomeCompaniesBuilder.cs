using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.Home;

/// <summary>
/// «Tus empresas» de Inicio-Miembro: las empresas donde la persona tiene permisos, según
/// <see cref="IPermissionService.GetPermissionsByCompanyAsync"/>, con sus roles y los miembros si puede verlos. Los miembros
/// de todas las empresas se cuentan en una sola consulta.
/// </summary>
internal sealed class MemberHomeCompaniesBuilder(ICompanyMembershipReader memberships)
{
    public async Task<IReadOnlyList<HomeCompanyCard>> ListAsync(Guid userId,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> permissionsByCompany, CancellationToken cancellationToken)
    {
        var companies = (await memberships.ListActiveForUserAsync(userId, cancellationToken))
            .Where(company => permissionsByCompany.TryGetValue(company.CompanyId, out var granted) && granted.Count > 0)
            .ToArray();
        Guid[] counted = [.. companies.Select(company => company.CompanyId)
            .Where(id => permissionsByCompany[id].Contains(Permissions.Company.Members.Read, StringComparer.Ordinal))];
        var members = counted.Length == 0 ? null : await memberships.CountActiveMembersAsync(counted, cancellationToken);
        return [.. companies.Select(company => new HomeCompanyCard(company.CompanyId, company.CompanyName,
            [.. company.Roles.Select(role => RoleTexts.Name(role.Name))],
            members is not null && counted.Contains(company.CompanyId) ? members.GetValueOrDefault(company.CompanyId) : null))];
    }
}
