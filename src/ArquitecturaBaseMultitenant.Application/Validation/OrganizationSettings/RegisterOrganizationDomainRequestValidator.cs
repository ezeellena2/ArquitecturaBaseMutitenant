using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationSettings;

internal sealed class RegisterOrganizationDomainRequestValidator : AbstractValidator<RegisterOrganizationDomainRequest>
{
    public RegisterOrganizationDomainRequestValidator()
    {
        RuleFor(request => request.Domain).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .Must(value => OrganizationEmailDomain.Create(value).IsSuccess)
            .WithMessage(_ => ValidationTexts.DomainInvalid);
    }
}
