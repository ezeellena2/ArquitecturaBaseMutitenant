using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>Las listas de las fuentes ordenan solo por nombre.</summary>
internal sealed class ListFormCatalogLookupsRequestValidator : PagedRequestValidator<ListFormCatalogLookupsRequest>
{
    public ListFormCatalogLookupsRequestValidator() : base(ListFormCatalogLookupsRequest.SortableFields)
    {
    }
}
