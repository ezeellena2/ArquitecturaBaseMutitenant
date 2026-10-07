namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Sitio publicado del subdominio de la petición; no concede acceso a datos privados.</summary>
public interface IPublicSiteContext
{
    Guid? BusinessTenantId { get; }

    string? Slug { get; }
}
