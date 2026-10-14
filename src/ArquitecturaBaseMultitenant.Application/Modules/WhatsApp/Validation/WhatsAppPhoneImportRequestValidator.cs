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
        // Los roles son de la empresa elegida: sin empresa no hay roles.
        RuleFor(request => request.RoleIds).Must((request, ids) => WhatsAppRequestRules.AreValidIds(ids) && (request.CompanyId is not null || ids!.Count == 0))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.CompanyId).Must(id => id != Guid.Empty).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}
