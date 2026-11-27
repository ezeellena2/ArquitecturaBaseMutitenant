using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>Las opciones van siempre por posición: el orden de las opciones es un dato y no se elige (264).</summary>
internal sealed class ListFormCatalogItemsRequestValidator : PagedRequestValidator<ListFormCatalogItemsRequest>
{
    public ListFormCatalogItemsRequestValidator() : base([])
    {
        RuleFor(request => request.Status).IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
