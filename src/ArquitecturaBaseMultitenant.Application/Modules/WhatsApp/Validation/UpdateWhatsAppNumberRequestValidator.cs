using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class UpdateWhatsAppNumberRequestValidator : AbstractValidator<UpdateWhatsAppNumberRequest>
{
    public UpdateWhatsAppNumberRequestValidator()
    {
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
