using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class ConnectPlatformWhatsAppNumberRequestValidator : AbstractValidator<ConnectPlatformWhatsAppNumberRequest>
{
    public ConnectPlatformWhatsAppNumberRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Code).MaximumLength(WhatsAppTextLimits.SignupCode).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.PhoneNumberId).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.PhoneNumberId).MaximumLength(WhatsAppTextLimits.MetaIdentifier).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.WabaId).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.WabaId).MaximumLength(WhatsAppTextLimits.MetaIdentifier).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
    }
}
