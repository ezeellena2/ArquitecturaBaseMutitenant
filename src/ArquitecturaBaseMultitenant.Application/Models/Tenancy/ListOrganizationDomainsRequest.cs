using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

public sealed record ListOrganizationDomainsRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["domain"];
    /// <summary>Vista previa interna de dominios pendientes de Inicio; no se expone por HTTP.</summary>
    public bool PendingPreview { get; init; }
    public TenantDomainStatus? Status { get; init; }
}
