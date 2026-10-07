namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

/// <summary>Identificación pública de una organización publicada y activa.</summary>
public sealed record PublicSiteRow(Guid BusinessTenantId, string Slug);
