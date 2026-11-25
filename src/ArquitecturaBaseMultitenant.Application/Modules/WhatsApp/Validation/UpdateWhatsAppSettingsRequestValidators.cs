using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

/// <summary>
/// La configuración de WhatsApp de la plataforma (270 y 272): al menos un país, de los habilitados y sin repetir, y los dos
/// plazos entre 1 y 3650 días.
/// </summary>
internal sealed class UpdatePlatformWhatsAppSettingsRequestValidator : AbstractValidator<UpdatePlatformWhatsAppSettingsRequest>
{
    public UpdatePlatformWhatsAppSettingsRequestValidator(ICountryCatalog countries)
    {
        RuleFor(request => request.AllowedCountries).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"))
            .Must(list => list is null || list.Count <= WhatsAppPlatformSettings.MaxAllowedCountries
                && list.Distinct(StringComparer.Ordinal).Count() == list.Count && list.All(WhatsAppPlatformSettings.IsCountryCode))
            .WithMessage(_ => ValidationTexts.CountryInvalid)
            .MustAsync(async (list, ct) => list is null || await AllEnabledAsync(countries, list, ct))
            .WithMessage(_ => ValidationTexts.CountryInvalid);
        RuleFor(request => request.ChatRetentionDays).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"))
            .InclusiveBetween(WhatsAppSettingsLimits.MinChatRetentionDays, WhatsAppSettingsLimits.MaxChatRetentionDays)
            .WithMessage(_ => ValidationTexts.NumberRange);
        RuleFor(request => request.MaxOrganizationChatRetentionDays)
            .NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"))
            .InclusiveBetween(WhatsAppSettingsLimits.MinChatRetentionDays, WhatsAppSettingsLimits.MaxChatRetentionDays)
            .WithMessage(_ => ValidationTexts.NumberRange);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }

    private static async Task<bool> AllEnabledAsync(ICountryCatalog catalog, IReadOnlyList<string> codes, CancellationToken ct)
    {
        foreach (var code in codes)
            if (code is null || (await catalog.FindAsync(code, ct))?.IsEnabled != true) return false;
        return true;
    }
}

/// <summary>
/// El plazo de los chats de la organización (272): de 1 a 3650 días acá; el servicio lo compara además con el máximo que fija
/// la plataforma, que se lee en el momento.
/// </summary>
internal sealed class UpdateWhatsAppOrganizationSettingsRequestValidator : AbstractValidator<UpdateWhatsAppOrganizationSettingsRequest>
{
    public UpdateWhatsAppOrganizationSettingsRequestValidator()
    {
        RuleFor(request => request.ChatRetentionDays).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"))
            .InclusiveBetween(WhatsAppSettingsLimits.MinChatRetentionDays, WhatsAppSettingsLimits.MaxChatRetentionDays)
            .WithMessage(_ => ValidationTexts.NumberRange);
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
