using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.OrganizationSettings;

internal sealed class UpdateOrganizationSettingsRequestValidator : AbstractValidator<UpdateOrganizationSettingsRequest>
{
    public UpdateOrganizationSettingsRequestValidator(ICultureCatalog cultures, ITimeZoneCatalog timeZones,
        ICurrencyCatalog currencies)
    {
        RuleFor(request => request.Name).OrganizationName();
        RuleFor(request => request.DefaultCulture).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required).ValidCulture(cultures);
        RuleFor(request => request.DefaultTimeZoneId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required).ValidTimeZone(timeZones);
        RuleFor(request => request.DefaultCurrency).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required).ValidCurrency(currencies);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required);
    }
}
