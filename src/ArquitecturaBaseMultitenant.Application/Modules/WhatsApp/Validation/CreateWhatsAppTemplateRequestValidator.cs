using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class CreateWhatsAppTemplateRequestValidator : AbstractValidator<CreateWhatsAppTemplateRequest>
{
    public CreateWhatsAppTemplateRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Name).MaximumLength(TextLimits.ShortName).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Category).Must(value => value is WhatsAppTemplateCategory.Utility or WhatsAppTemplateCategory.Marketing).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.Language).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Language).MaximumLength(TextLimits.ShortCode).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Body).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Body).MaximumLength(WhatsAppTextLimits.TemplateBody).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Variables).Must(WhatsAppRequestRules.AreValidTemplateVariables).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VariablesInvalid"));
    }
}
