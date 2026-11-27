namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>Roles, empresas, etiquetas y ejemplos: paginados, con búsqueda; <c>sort</c> solo acepta name.</summary>
public sealed record FormCatalogLookupHttpQuery(int? Page, int? PageSize, string? Search, string? Sort);
