using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Companies;

internal sealed class ListCompaniesRequestValidator : PagedRequestValidator<ListCompaniesRequest>
{
    public ListCompaniesRequestValidator() : base(ListCompaniesRequest.SortableFields)
    {
        RuleFor(request => request.Status).Must(status => status is null || Enum.IsDefined(status.Value))
            .WithMessage(_ => ValidationTexts.FilterInvalid);
    }
}
