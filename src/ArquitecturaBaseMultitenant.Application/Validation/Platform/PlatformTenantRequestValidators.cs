using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
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

internal sealed class CreatePlatformTenantRequestValidator : AbstractValidator<CreatePlatformTenantRequest>
{
    public CreatePlatformTenantRequestValidator(ICultureCatalog cultures, ITimeZoneCatalog timeZones)
    {
        RuleFor(r => r.Name).OrganizationName();
        RuleFor(r => r.FirstCompanyName).OrganizationName();
        RuleFor(r => r.OwnerEmail).NotNull().WithMessage(_ => ValidationTexts.Required);
        RuleFor(r => r.Culture).Required().ValidCulture(cultures);
        RuleFor(r => r.TimeZoneId).Required().ValidTimeZone(timeZones);
    }
}
