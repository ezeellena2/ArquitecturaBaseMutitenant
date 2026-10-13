using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class ImportWhatsAppContactsRequestValidator : AbstractValidator<ImportWhatsAppContactsRequest>
{
    public ImportWhatsAppContactsRequestValidator()
    {
        RuleFor(request => request.Content).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.FileRequired"));
        RuleFor(request => request.Tag).MaximumLength(WhatsAppTextLimits.Tag).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
    }
}
