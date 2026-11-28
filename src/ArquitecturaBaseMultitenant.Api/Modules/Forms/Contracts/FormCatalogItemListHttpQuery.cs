using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>Las opciones de un catálogo a mano, por posición; <c>ids</c>, repetido, trae solo esas (E13, hasta 100 y sin vacíos).</summary>
public sealed record FormCatalogItemListHttpQuery(int? Page, int? PageSize, string? Search, FormCatalogStatusFilter? Status, IReadOnlyList<Guid>? Ids);
