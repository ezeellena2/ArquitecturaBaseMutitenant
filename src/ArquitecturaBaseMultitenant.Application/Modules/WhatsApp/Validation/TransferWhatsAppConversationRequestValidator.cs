using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

internal sealed class TransferWhatsAppConversationRequestValidator : AbstractValidator<TransferWhatsAppConversationRequest>
{
    public TransferWhatsAppConversationRequestValidator()
    {
        RuleFor(request => request).Must(request => request.TeamId is not null ^ request.MemberId is not null).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TransferTargetRequired"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.VersionRequired"));
    }
}
