using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

public sealed record UpdatePublicPageRequest(string? Slug, string? Name, string? Description,
    Email? ContactEmail, string? ContactPhone, string? Address, string? Website,
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
