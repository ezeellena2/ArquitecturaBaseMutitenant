using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms.Contracts;

/// <summary>La plantilla para importar, con la columna del valor del tipo pedido (C10).</summary>
public sealed record FormCatalogTemplateHttpQuery(FormCatalogValueType? ValueType);
