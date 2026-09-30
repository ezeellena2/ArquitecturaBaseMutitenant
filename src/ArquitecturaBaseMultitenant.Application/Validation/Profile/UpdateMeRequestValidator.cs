using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Validation.Profile;

/// <summary>Exige versión para concurrencia y preferencias habilitadas en los catálogos antes de actualizar el perfil.</summary>
internal sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator(ICultureCatalog cultures, ITimeZoneCatalog timeZones)
    {
        RuleFor(request => request.Version).NotNull().WithMessage(_ => ValidationTexts.Required)
            .GreaterThan(0U).WithMessage(_ => ValidationTexts.Required);
        RuleFor(request => request.DisplayName).MaxLength(TextLimits.PersonName);
        RuleFor(request => request.Culture)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .ValidCulture(cultures);
        RuleFor(request => request.TimeZoneId)
            .NotEmpty().WithMessage(_ => ValidationTexts.Required)
            .ValidTimeZone(timeZones);
    }
}
