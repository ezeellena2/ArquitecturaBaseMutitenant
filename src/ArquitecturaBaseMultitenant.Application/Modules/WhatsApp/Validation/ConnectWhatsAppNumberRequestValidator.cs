using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class ConnectWhatsAppNumberRequestValidator : AbstractValidator<ConnectWhatsAppNumberRequest>
{
    public ConnectWhatsAppNumberRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Code).MaximumLength(WhatsAppTextLimits.SignupCode).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        // En coexistencia el alta trae solo la cuenta: el número se resuelve desde ella.
        RuleFor(request => request.PhoneNumberId).NotEmpty().When(request => request.Coexistence != true).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.PhoneNumberId).MaximumLength(WhatsAppTextLimits.MetaIdentifier).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.WabaId).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.WabaId).MaximumLength(WhatsAppTextLimits.MetaIdentifier).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
    }
}
