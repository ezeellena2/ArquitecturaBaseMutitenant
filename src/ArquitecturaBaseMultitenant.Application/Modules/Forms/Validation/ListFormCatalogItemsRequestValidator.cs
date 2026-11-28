using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

/// <summary>
/// Las opciones van siempre por posición: el orden de las opciones es un dato y no se elige (264). <c>ids</c> (E13) nombra hasta 100
/// opciones, sin identidades vacías.
/// </summary>
internal sealed class ListFormCatalogItemsRequestValidator : PagedRequestValidator<ListFormCatalogItemsRequest>
{
    public ListFormCatalogItemsRequestValidator() : base([])
    {
        RuleFor(request => request.Status).IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Ids).Must(ids => ids is null || ids.Count <= FormCatalogLimits.MaxItemIds)
            .WithMessage(_ => FormCatalogInputRules.TooMany);
        RuleForEach(request => request.Ids).NotEqual(Guid.Empty).WithMessage(_ => ValidationTexts.FilterInvalid)
            .When(request => request.Ids is not { Count: > FormCatalogLimits.MaxItemIds });
    }
}
