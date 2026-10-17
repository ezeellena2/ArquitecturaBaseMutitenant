using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

/// <summary>
/// El nombre tiene que dejar un nombre para Meta (letras o números); la cultura, ser del catálogo; y los datos del texto,
/// numerados desde {{1}} sin saltear ninguno, con un nombre y un ejemplo cada uno.
/// </summary>
internal sealed class CreateWhatsAppTemplateRequestValidator : AbstractValidator<CreateWhatsAppTemplateRequest>
{
    public CreateWhatsAppTemplateRequestValidator(ICultureCatalog cultures)
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Name).MaximumLength(TextLimits.ShortName).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Name).Must(name => string.IsNullOrWhiteSpace(name) || WhatsAppTemplateBase.ToMetaName(name) is not null)
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Category).Must(value => value is WhatsAppTemplateCategory.Utility or WhatsAppTemplateCategory.Marketing).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.Language).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Language).MaximumLength(TextLimits.ShortCode).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Language).ValidCulture(cultures);
        RuleFor(request => request.Body).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Body).MaximumLength(WhatsAppTextLimits.TemplateBody).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Variables).Must(WhatsAppRequestRules.AreValidTemplateVariables).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VariablesInvalid"));
        RuleFor(request => request.Variables).Must((request, variables) => string.IsNullOrEmpty(request.Body)
                || WhatsAppTemplateBase.Placeholders(request.Body) == (variables?.Count ?? 0))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VariablesInvalid"));
    }
}
