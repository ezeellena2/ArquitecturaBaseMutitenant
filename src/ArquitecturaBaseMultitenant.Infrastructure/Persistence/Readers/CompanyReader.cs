using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta empresas y conteos de membresías bajo el tenant activo.</summary>
internal sealed class CompanyReader(ApplicationDbContext context, ITenantContext tenantContext) : ICompanyReader
{
    private static readonly SortMap<Company> Sort = new()
    {
        ["name"] = company => company.Name,
        ["createdAtUtc"] = company => company.CreatedAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public Task<PagedResult<CompanyRow>> ListAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var companies = Companies(tenantId, request)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, company => company.Id);
        return ProjectPageAsync(companies, tenantId, request, cancellationToken);
    }

    public Task<CompanyRow?> FindByIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return Project(context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId
            && company.Id == companyId)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CompanyFilterCountsResponse> GetFilterCountsAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var companies = Companies(tenantId, request with { Status = null });
        return new CompanyFilterCountsResponse(
            await companies.CountAsync(cancellationToken),
            await companies.CountAsync(company => company.Status == CompanyStatus.Active, cancellationToken),
            await companies.CountAsync(company => company.Status == CompanyStatus.Inactive, cancellationToken));
    }

    public Task<bool> ExistsTaxIdAsync(string country, string type, string number, Guid? excludedCompanyId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        var tenantId = tenantContext.RequiredTenantId;
        return context.Companies.AsNoTracking().AnyAsync(company => company.TenantId == tenantId
            && company.TaxCountry == country && company.TaxType == type && company.TaxNumber == number
            && (excludedCompanyId == null || company.Id != excludedCompanyId), cancellationToken);
    }

    private IQueryable<Company> Companies(Guid tenantId, ListCompaniesRequest request)
    {
        var companies = context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId)
            .ApplySearch(request.Search, company => company.Name, company => company.TaxNumber);
        if (request.Status is { } status) companies = companies.Where(company => company.Status == status);
        return companies;
    }

    private IQueryable<CompanyRow> Project(IQueryable<Company> companies) => companies.Select(company => new CompanyRow(
        company.Id,
        company.Name,
        company.TaxCountry,
        company.TaxType,
        company.TaxNumber,
        company.TimeZoneId,
        company.Status,
        context.CompanyMemberships.Count(membership => membership.TenantId == company.TenantId
            && membership.CompanyId == company.Id && membership.Status == CompanyMembershipStatus.Active
            && context.Members.Any(member => member.TenantId == membership.TenantId
                && member.Id == membership.MemberId && member.Status == MemberStatus.Active)),
        company.Version,
        company.CreatedAtUtc));

    private async Task<PagedResult<CompanyRow>> ProjectPageAsync(IQueryable<Company> companies, Guid tenantId,
        ListCompaniesRequest request, CancellationToken cancellationToken)
    {
        var page = await companies.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<CompanyRow>([], page.Page, page.PageSize, page.TotalCount);

        var companyIds = page.Items.Select(company => company.Id).ToArray();
        var rows = await Project(context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId
                && companyIds.Contains(company.Id)))
            .ToDictionaryAsync(company => company.Id, cancellationToken);
        return new PagedResult<CompanyRow>([.. page.Items.Select(company => rows[company.Id])],
            page.Page, page.PageSize, page.TotalCount);
    }
}
