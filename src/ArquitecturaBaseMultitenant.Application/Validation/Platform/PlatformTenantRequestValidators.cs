using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Platform;

internal sealed class ListPlatformTenantsRequestValidator : PagedRequestValidator<ListPlatformTenantsRequest>
{
    public ListPlatformTenantsRequestValidator() : base(ListPlatformTenantsRequest.SortableFields)
    {
        RuleFor(r => r.Status).IsInEnum().When(r => r.Status is not null).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
        RuleFor(r => r.Source).IsInEnum().When(r => r.Source is not null).WithMessage(_ => ValidationTexts.RegistrationModeInvalid);
    }
}

internal sealed class PlatformTenantStateRequestValidator : AbstractValidator<PlatformTenantStateRequest>
{
    public PlatformTenantStateRequestValidator()
    {
        RuleFor(r => r.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(r => r.Reason).Required().Description();
    }
}
