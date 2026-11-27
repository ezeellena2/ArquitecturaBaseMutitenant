using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;

internal sealed class ListFormCatalogsRequestValidator : PagedRequestValidator<ListFormCatalogsRequest>
{
    public ListFormCatalogsRequestValidator() : base(ListFormCatalogsRequest.SortableFields)
    {
        RuleFor(request => request.Source).IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(request => request.Status).IsInEnum().WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
