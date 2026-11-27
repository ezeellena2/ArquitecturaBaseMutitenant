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
    private static readonly SortMap<CompanySortRow> Sort = new()
    {
        ["name"] = company => company.Name,
        ["taxId"] = company => company.TaxNumber,
        ["timeZoneId"] = company => company.TimeZoneId,
        ["memberCount"] = company => company.MemberCount,
        ["status"] = company => company.Status,
        ["createdAtUtc"] = company => company.CreatedAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public Task<PagedResult<CompanyRow>> ListAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var companies = Companies(tenantId, request)
            .Select(company => new CompanySortRow
            {
                Id = company.Id,
                Name = company.Name,
                TaxNumber = company.TaxNumber,
                TimeZoneId = company.TimeZoneId,
                MemberCount = context.CompanyMemberships.Count(membership => membership.TenantId == company.TenantId
                    && membership.CompanyId == company.Id && membership.Status == CompanyMembershipStatus.Active
                    && context.Members.Any(member => member.TenantId == membership.TenantId
                        && member.Id == membership.MemberId && member.Status == MemberStatus.Active)),
                Status = company.Status,
                CreatedAtUtc = company.CreatedAtUtc,
            })
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, company => company.Id)
            .Select(company => company.Id);
        return ProjectPageAsync(companies, tenantId, request, cancellationToken);
    }

    public Task<CompanyRow?> FindByIdAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return Project(context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId
            && company.Id == companyId)).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CompanyFilterCountsResponse> CountFiltersAsync(ListCompaniesRequest request,
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

    public Task<bool> IsActiveAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return context.Companies.AsNoTracking().AnyAsync(company => company.TenantId == tenantId
            && company.Id == companyId && company.Status == CompanyStatus.Active, cancellationToken);
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

    private async Task<PagedResult<CompanyRow>> ProjectPageAsync(IQueryable<Guid> companies, Guid tenantId,
        ListCompaniesRequest request, CancellationToken cancellationToken)
    {
        var page = await companies.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<CompanyRow>([], page.Page, page.PageSize, page.TotalCount);

        var companyIds = page.Items.ToArray();
        var rows = await Project(context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId
                && companyIds.Contains(company.Id)))
            .ToDictionaryAsync(company => company.Id, cancellationToken);
        return new PagedResult<CompanyRow>([.. page.Items.Select(companyId => rows[companyId])],
            page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>Una empresa con lo que se ordena: sus datos y cuántos miembros activos tiene, la cuenta de su fila.</summary>
    private sealed class CompanySortRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? TaxNumber { get; init; }
        public string TimeZoneId { get; init; } = string.Empty;
        public int MemberCount { get; init; }
        public CompanyStatus Status { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
