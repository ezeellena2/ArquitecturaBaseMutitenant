using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Auth;

internal sealed class BusinessSignupRequestValidator : AbstractValidator<BusinessSignupRequest>
{
    public BusinessSignupRequestValidator()
    {
        RuleFor(request => request.OrganizationName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.Slug).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(63).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.FirstCompanyName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.TaxId).MaximumLength(32).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.AcceptedTerms).Equal(true).WithMessage(_ => ValidationTexts.Required);
    }
}
