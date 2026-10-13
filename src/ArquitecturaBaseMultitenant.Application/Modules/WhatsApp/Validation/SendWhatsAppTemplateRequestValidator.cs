using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class SendWhatsAppTemplateRequestValidator : AbstractValidator<SendWhatsAppTemplateRequest>
{
    public SendWhatsAppTemplateRequestValidator()
    {
        RuleFor(request => request.TemplateId).Must(value => value is not null && value != Guid.Empty).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Variables).Must(WhatsAppRequestRules.AreValidVariables).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VariablesInvalid"));
    }
}
