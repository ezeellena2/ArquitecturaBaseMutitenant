using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>El listado: <c>sort</c> es name, source, valueType, optionsCount o status, con <c>-</c> delante el otro sentido (264).</summary>
public sealed record FormCatalogListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search, FormCatalogSourceFilter? Source,
    FormCatalogStatusFilter? Status);
