using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
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
            .MaximumLength(TenantSlug.MaxLength).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.FirstCompanyName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);
        When(request => request.TaxId is not null, () =>
        {
            RuleFor(request => request.TaxId!.Type).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(_ => ValidationTexts.Required)
                .MaximumLength(TextLimits.TaxIdType).WithMessage(_ => ValidationTexts.MaxLength);
            RuleFor(request => request.TaxId!.Number).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(_ => ValidationTexts.Required)
                .MaximumLength(TextLimits.TaxIdNumber).WithMessage(_ => ValidationTexts.MaxLength);
        });
        RuleFor(request => request.AcceptedTerms).Equal(true).WithMessage(_ => ValidationTexts.Required);
    }
}
