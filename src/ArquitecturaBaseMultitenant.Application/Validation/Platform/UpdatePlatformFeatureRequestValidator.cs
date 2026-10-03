using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Features;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class UpdatePlatformFeatureRequestValidator : AbstractValidator<UpdatePlatformFeatureRequest>
{
    public UpdatePlatformFeatureRequestValidator()
    {
        RuleFor(request => request.Enabled).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.Source).NotNull().WithMessage(_ => ValidationTexts.Required)
            .Must(source => source is null || Enum.IsDefined(source.Value)).WithMessage(_ => ErrorTexts.Get(TenantFeatureErrors.InvalidSourceCode));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.ExpiresAtUtc).NotNull().When(request => request.Source == TenantFeatureSource.Trial)
            .WithMessage(_ => ErrorTexts.Get(TenantFeatureErrors.ExpiryRequiredCode));
        RuleFor(request => request.Reason).Required().Description();
    }
}
