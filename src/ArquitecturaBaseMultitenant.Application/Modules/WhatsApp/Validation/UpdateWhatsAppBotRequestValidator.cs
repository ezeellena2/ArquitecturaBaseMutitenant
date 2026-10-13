using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class UpdateWhatsAppBotRequestValidator : AbstractValidator<UpdateWhatsAppBotRequest>
{
    public UpdateWhatsAppBotRequestValidator()
    {
        RuleFor(request => request.Welcome).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.Welcome).MaximumLength(WhatsAppTextLimits.BotText).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.OutOfHours).NotEmpty().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.Required"));
        RuleFor(request => request.OutOfHours).MaximumLength(WhatsAppTextLimits.BotText).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Schedule).Must(WhatsAppRequestRules.IsValidSchedule).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.ScheduleInvalid"));
        RuleFor(request => request.Menu).Must(WhatsAppRequestRules.IsValidMenu).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.MenuInvalid"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
