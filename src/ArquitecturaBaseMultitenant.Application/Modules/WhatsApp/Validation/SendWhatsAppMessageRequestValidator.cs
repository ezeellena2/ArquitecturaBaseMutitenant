using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class SendWhatsAppMessageRequestValidator : AbstractValidator<SendWhatsAppMessageRequest>
{
    public SendWhatsAppMessageRequestValidator()
    {
        RuleFor(request => request.Text).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Text).MaximumLength(WhatsAppTextLimits.MessageText).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
    }
}
