using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.Common;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class CreateWhatsAppBroadcastRequestValidator : AbstractValidator<CreateWhatsAppBroadcastRequest>
{
    public CreateWhatsAppBroadcastRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Name).MaximumLength(TextLimits.ShortName).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.TemplateId).Must(value => value is not null && value != Guid.Empty).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Variables).Must(WhatsAppRequestRules.AreValidVariables).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VariablesInvalid"));
        RuleFor(request => request.Audience).Must(audience => audience is not null && WhatsAppRequestRules.IsValidAudience(audience.Kind, audience.Tag)).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.ScheduledAtUtc).Must(value => value is null || value.Value.Kind == DateTimeKind.Utc).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.ScheduleInPast"));
    }
}
