namespace ArquitecturaBaseMultitenant.Application.Models.PublicSite;

/// <summary>Tarjeta del directorio; contiene únicamente los cuatro datos públicos del listado.</summary>
public sealed record DirectoryEntryRow(string Slug, string Name, string? Description, string? LogoUrl);
