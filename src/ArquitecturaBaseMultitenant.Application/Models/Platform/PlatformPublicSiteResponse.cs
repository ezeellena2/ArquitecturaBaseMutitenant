namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Solo metadatos de publicación; no incluye contenido, logo ni contacto de la página.</summary>
public sealed record PlatformPublicSiteResponse(string? Slug, string Name, bool IsPublished,
    DateTime? PublishBlockedAtUtc, string? PublishBlockedReason, uint Version, DateTime? PublishedAtUtc = null);
