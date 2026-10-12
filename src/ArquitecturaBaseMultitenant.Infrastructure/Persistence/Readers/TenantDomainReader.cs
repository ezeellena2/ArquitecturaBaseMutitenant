using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// Expone únicamente los dominios activos de la organización técnica seleccionada, con cuántos correos verificados de
/// ese dominio administra. Lee los métodos de ingreso globales solo para contarlos.
/// </summary>
internal sealed class TenantDomainReader(ApplicationDbContext context, ITenantContext tenantContext)
    : ITenantDomainReader
{
    private static readonly SortMap<TenantDomain> Sort = new() { ["domain"] = domain => domain.Domain };

    public async Task<PagedResult<OrganizationDomainRow>> ListPagedAsync(ListOrganizationDomainsRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.RequiredTenantId;
        var domains = context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Status != TenantDomainStatus.Removed)
            .ApplySearch(request.Search, domain => domain.Domain);
        if (request.Status is { } status)
            domains = domains.Where(domain => domain.Status == status);
        if (request.PendingPreview)
        {
            domains = domains.Where(domain => domain.Status == TenantDomainStatus.Pending);
            request = request with { Page = PagedRequest.DefaultPage, PageSize = PendingGroup.PreviewLimit };
        }
        var sorted = request.PendingPreview
            ? domains.OrderBy(domain => domain.CreatedAtUtc).ThenBy(domain => domain.Id)
            : domains.ApplySort(SortDescriptor.Parse(request.Sort), Sort, new SortDescriptor("domain", false), domain => domain.Id);
        return await sorted
            .Select(domain => new OrganizationDomainRow(domain.Id, domain.Domain, domain.Status,
                domain.VerificationToken, domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version,
                context.LoginMethods.Count(method => method.Type == LoginMethodType.Email
                    && method.ManagedByTenantId == organizationId && method.VerifiedAtUtc != null
                    && method.Value.EndsWith("@" + domain.Domain))))
            .ToPagedResultAsync(request, cancellationToken);
    }

    public Task<string?> FindHomeDomainAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.RequiredTenantId;
        return context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Status != TenantDomainStatus.Removed)
            .OrderBy(domain => domain.Status != TenantDomainStatus.Verified).ThenBy(domain => domain.Domain).ThenBy(domain => domain.Id)
            .Select(domain => domain.Domain).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationDomainRow>> ListCurrentAsync(CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.RequiredTenantId;
        return await context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Status != TenantDomainStatus.Removed)
            .OrderBy(domain => domain.Domain)
            .Select(domain => new OrganizationDomainRow(domain.Id, domain.Domain, domain.Status,
                domain.VerificationToken, domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version,
                context.LoginMethods.Count(method => method.Type == LoginMethodType.Email
                    && method.ManagedByTenantId == organizationId && method.VerifiedAtUtc != null
                    && method.Value.EndsWith("@" + domain.Domain))))
            .ToArrayAsync(cancellationToken);
    }

    public Task<OrganizationDomainRow?> FindCurrentByIdAsync(Guid domainId,
        CancellationToken cancellationToken)
    {
        if (domainId == Guid.Empty) throw new ArgumentException("A domain ID is required.", nameof(domainId));
        var organizationId = tenantContext.RequiredTenantId;
        return context.TenantDomains.AsNoTracking()
            .Where(domain => domain.OrganizationId == organizationId && domain.Id == domainId
                && domain.Status != TenantDomainStatus.Removed)
            .Select(domain => new OrganizationDomainRow(domain.Id, domain.Domain, domain.Status,
                domain.VerificationToken, domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version,
                context.LoginMethods.Count(method => method.Type == LoginMethodType.Email
                    && method.ManagedByTenantId == organizationId && method.VerifiedAtUtc != null
                    && method.Value.EndsWith("@" + domain.Domain))))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
