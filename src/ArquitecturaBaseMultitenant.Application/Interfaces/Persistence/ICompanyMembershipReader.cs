using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Companies;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta miembros de una empresa del tenant activo sin exponer entidades seguidas.</summary>
public interface ICompanyMembershipReader
{
    Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId, ListCompanyMembersRequest request,
        CancellationToken cancellationToken);
    Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Los miembros activos de cada empresa en una sola consulta: el conteo «Activos» de <see cref="GetFilterCountsAsync"/>
    /// sin filtros. Una empresa sin miembros activos no figura.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveMembersAsync(IReadOnlyCollection<Guid> companyIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<UserCompanyRow>> ListActiveForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Las personas activas de la organización sin membresía (en ningún estado) en esa empresa.</summary>
    Task<PagedResult<CompanyCandidateRow>> ListCandidatesAsync(Guid companyId, ListCompanyCandidatesRequest request,
        CancellationToken cancellationToken);
}
