using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class CreateWhatsAppTeamRequestValidator : AbstractValidator<CreateWhatsAppTeamRequest>
{
    public CreateWhatsAppTeamRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Name).MaximumLength(TextLimits.ShortName).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Inbox).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.MemberIds).Must(WhatsAppRequestRules.AreValidMembers).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.NotifyAfterMinutes).Must(WhatsAppRequestRules.IsValidNotice).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.NotifyInvalid"));
    }
}
