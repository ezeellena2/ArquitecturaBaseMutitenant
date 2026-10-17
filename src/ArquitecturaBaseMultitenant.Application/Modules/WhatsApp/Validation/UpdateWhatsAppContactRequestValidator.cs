using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class UpdateWhatsAppContactRequestValidator : AbstractValidator<UpdateWhatsAppContactRequest>
{
    public UpdateWhatsAppContactRequestValidator()
    {
        RuleFor(request => request.Name).MaximumLength(TextLimits.PersonName).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Tags).Must(WhatsAppRequestRules.AreValidTags).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TagsInvalid"));
        RuleFor(request => request.Consent).Must(value => value is null || Enum.IsDefined(value.Value)).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
