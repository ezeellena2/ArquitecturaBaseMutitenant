using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Companies;

internal sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.TimeZoneId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request).Must(TaxIdIsComplete).WithMessage(_ => ValidationTexts.Required);
    }

    private static bool TaxIdIsComplete(CreateCompanyRequest request) =>
        string.IsNullOrWhiteSpace(request.TaxIdType) == string.IsNullOrWhiteSpace(request.TaxIdNumber);
}

internal sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .MaximumLength(TextLimits.OrganizationName).WithMessage(_ => ValidationTexts.MaxLength);
        RuleFor(request => request.TimeZoneId).NotEmpty().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request).Must(TaxIdIsComplete).WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }

    private static bool TaxIdIsComplete(UpdateCompanyRequest request) =>
        string.IsNullOrWhiteSpace(request.TaxIdType) == string.IsNullOrWhiteSpace(request.TaxIdNumber);
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
