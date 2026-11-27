using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>Las opciones de un catálogo a mano, por posición.</summary>
public sealed record FormCatalogItemListHttpQuery(int? Page, int? PageSize, string? Search, FormCatalogStatusFilter? Status);
