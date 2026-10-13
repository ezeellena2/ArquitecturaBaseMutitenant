using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class WhatsAppPhoneImportRequestValidator : AbstractValidator<WhatsAppPhoneImportRequest>
{
    public WhatsAppPhoneImportRequestValidator()
    {
        RuleFor(request => request.Phones).Must(values => values is { Count: > 0 and <= WhatsAppFeature.MaxPhoneImport } && values.All(value => value is not null)).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.PhonesInvalid"));
        RuleFor(request => request.RoleIds).Must(WhatsAppRequestRules.AreValidIds).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}
