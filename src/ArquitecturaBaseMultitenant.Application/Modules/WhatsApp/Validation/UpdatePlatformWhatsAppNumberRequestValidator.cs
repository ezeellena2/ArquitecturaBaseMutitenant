using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class UpdatePlatformWhatsAppNumberRequestValidator : AbstractValidator<UpdatePlatformWhatsAppNumberRequest>
{
    public UpdatePlatformWhatsAppNumberRequestValidator()
    {
        RuleFor(request => request.ConsultationsEnabled).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
