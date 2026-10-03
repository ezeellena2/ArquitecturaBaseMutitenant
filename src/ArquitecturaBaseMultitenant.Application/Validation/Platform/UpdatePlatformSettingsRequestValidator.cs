using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class UpdatePlatformSettingsRequestValidator : AbstractValidator<UpdatePlatformSettingsRequest>
{
    public UpdatePlatformSettingsRequestValidator()
    {
        RuleFor(request => request.ConsumerSignup).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(mode => mode is null || Enum.IsDefined(mode.Value)).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
        RuleFor(request => request.BusinessSignup).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(mode => mode is null || Enum.IsDefined(mode.Value)).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
        RuleFor(request => request.MaxOwnedOrganizations).NotNull().WithMessage(_ => ValidationTexts.Required)
            .GreaterThanOrEqualTo(1).WithMessage(_ => ValidationTexts.OwnedOrganizationsMinimum);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Reason).Required().Description();
    }
}
