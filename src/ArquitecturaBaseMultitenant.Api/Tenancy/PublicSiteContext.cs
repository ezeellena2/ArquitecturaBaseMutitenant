using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Conserva el sitio público resuelto en este pedido, separado del acceso autenticado.</summary>
public sealed class PublicSiteContext : IPublicSiteContext
{
    public Guid? BusinessTenantId { get; private set; }

    public string? Slug { get; private set; }

    internal void Set(PublicSiteRow site)
    {
        BusinessTenantId = site.BusinessTenantId;
        Slug = site.Slug;
    }
}
