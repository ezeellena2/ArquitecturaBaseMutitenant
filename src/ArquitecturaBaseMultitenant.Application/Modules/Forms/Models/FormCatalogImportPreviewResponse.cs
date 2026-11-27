using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// La revisión de pegar o importar (C7), sin escribir: la versión leída, el SHA-256 de lo revisado y las cuatro listas paginadas
/// con el page y el pageSize del pedido. Las proporciones son fracciones.
/// </summary>
public sealed record FormCatalogImportPreviewResponse(uint Version, string Sha256, string? SheetName, int RowsRead, int Unchanged,
    decimal DeactivationShare, decimal ErrorShare, bool ConfirmDeactivations, bool ConfirmErrors,
    PagedResult<FormCatalogImportErrorRow> Errors, PagedResult<FormCatalogImportAddedRow> Added,
    PagedResult<FormCatalogImportChangedRow> Changed, PagedResult<FormCatalogImportDeactivatedRow> Deactivated);

public sealed record FormCatalogImportErrorRow(int Row, string? Column, string Code, string? Value, int? OtherRow);

public sealed record FormCatalogImportAddedRow(int Row, string Code, string Text);

public sealed record FormCatalogImportChangedRow(int Row, string Code, string Column, string? Before, string? After);

public sealed record FormCatalogImportDeactivatedRow(string Code, string Text);
