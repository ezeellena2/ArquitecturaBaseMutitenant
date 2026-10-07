using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

/// <summary>Contenido publicado del sitio; no expone identificadores ni estado de administración.</summary>
public sealed record PublicSiteResponse(string Slug, string Name, string? Description,
    string? LogoUrl, string? Address, PhoneNumber? Phone, Email? Email, string? Website);

/// <summary>Proyección de lectura pública; la versión se usa únicamente para invalidar el logo.</summary>
public sealed record PublicSitePageRow(string Name, string? Description, string? Address,
    PhoneNumber? Phone, Email? Email, string? Website, bool HasLogo, uint Version);

/// <summary>Imagen para la respuesta binaria HTTP; nunca se serializa como contenido de la página.</summary>
public sealed record PublicSiteLogoResponse(byte[] Bytes, string ContentType, uint Version);
