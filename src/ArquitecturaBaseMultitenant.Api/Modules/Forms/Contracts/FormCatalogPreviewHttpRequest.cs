using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>La vista previa de unos filtros sin guardar: la persona de ejemplo con Members y el cliente de ejemplo con Contacts.</summary>
public sealed record FormCatalogPreviewHttpRequest(FormCatalogSource? Source, FormCatalogFiltersHttpInput? Filters, Guid? SampleMemberId,
    Guid? SampleContactId);
