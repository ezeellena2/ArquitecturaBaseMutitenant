using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>
/// Solo dice si una empresa está activa, lo único que el <c>PermissionService</c> pregunta: para quien no es miembro de
/// esa empresa valen los roles de organización si está activa. Cuenta las preguntas; lo demás no se usa.
/// </summary>
internal sealed class CompanyActivityReader(params Guid[] active) : ICompanyReader
{
    public int Checks { get; private set; }

    public Task<bool> IsActiveAsync(Guid companyId, CancellationToken cancellationToken)
    {
        Checks++;
        return Task.FromResult(active.Contains(companyId));
    }

    public Task<PagedResult<CompanyRow>> ListAsync(ListCompaniesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<CompanyRow?> FindByIdAsync(Guid companyId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<CompanyFilterCountsResponse> CountFiltersAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> ExistsTaxIdAsync(string country, string type, string number, Guid? excludedCompanyId,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
