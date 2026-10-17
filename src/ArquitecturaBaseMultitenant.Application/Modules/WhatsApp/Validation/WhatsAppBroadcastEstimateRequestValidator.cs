using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class WhatsAppBroadcastEstimateRequestValidator : AbstractValidator<WhatsAppBroadcastEstimateRequest>
{
    public WhatsAppBroadcastEstimateRequestValidator()
    {
        RuleFor(request => request.TemplateId).Must(value => value is not null && value != Guid.Empty).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request).Must(request => WhatsAppRequestRules.IsValidAudience(request.Kind, request.Tag)).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}
