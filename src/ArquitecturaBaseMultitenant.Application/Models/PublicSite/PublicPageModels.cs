using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

/// <summary>El teléfono llega como país y lo escrito; el servicio lo interpreta y guarda E.164 (telefonos.md).</summary>
public sealed record UpdatePublicPageRequest(string? Slug, string? Name, string? Description,
    Email? ContactEmail, string? ContactPhoneCountry, string? ContactPhoneNumber, string? Address, string? Website,
    string? LogoBase64, string? LogoContentType, bool RemoveLogo, uint? Version);

public sealed record SetPublicPagePublicationRequest(uint? Version);

public sealed record PublicPageResponse(string? Slug, string Name, string? Description,
    string? LogoDataUri, Email? ContactEmail, string? ContactPhone, string? Address,
    string? Website, bool IsPublished, DateTime? PublishBlockedAtUtc,
    string? PublishBlockedReason, uint Version);

public sealed record PublicPageRow(string Name, string? Description, byte[]? LogoBytes,
    string? LogoContentType, Email? ContactEmail, string? ContactPhone, string? Address,
    string? Website, bool IsPublished, DateTime? PublishBlockedAtUtc,
    string? PublishBlockedReason, uint Version);
