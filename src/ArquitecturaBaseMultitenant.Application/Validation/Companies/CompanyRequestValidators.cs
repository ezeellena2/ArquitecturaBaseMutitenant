using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Companies;

internal sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator() => RuleFor(request => request.Name).OrganizationName();
}

internal sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(request => request.Name).OrganizationName();
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}

internal sealed class ChangeCompanyStatusRequestValidator : AbstractValidator<ChangeCompanyStatusRequest>
{
    public ChangeCompanyStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(status => status is not null && Enum.IsDefined(status.Value))
            .WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}
