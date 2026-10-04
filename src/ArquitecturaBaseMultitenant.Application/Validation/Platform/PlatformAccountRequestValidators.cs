using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class ListPlatformAccountsRequestValidator : PagedRequestValidator<ListPlatformAccountsRequest>
{
    public ListPlatformAccountsRequestValidator() : base(ListPlatformAccountsRequest.SortableFields)
    {
        RuleFor(r => r.State).IsInEnum().When(r => r.State is not null).WithMessage(_ => ValidationTexts.FilterInvalid);
        RuleFor(r => r.Kind).IsInEnum().When(r => r.Kind is not null).WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
