using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// La vista previa de unos filtros sin guardar: la persona de ejemplo con <see cref="FormCatalogSource.Members"/> y el cliente de
/// ejemplo con <see cref="FormCatalogSource.Contacts"/>, como destinatario.
/// </summary>
public sealed record FormCatalogPreviewRequest(FormCatalogSource? Source, FormCatalogFiltersInput? Filters, Guid? SampleMemberId,
    Guid? SampleContactId);
