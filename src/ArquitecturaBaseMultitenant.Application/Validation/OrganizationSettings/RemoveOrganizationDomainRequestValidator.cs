using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationSettings;

internal sealed class RemoveOrganizationDomainRequestValidator : AbstractValidator<RemoveOrganizationDomainRequest>
{
    public RemoveOrganizationDomainRequestValidator()
    {
        RuleFor(request => request.DomainId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}
