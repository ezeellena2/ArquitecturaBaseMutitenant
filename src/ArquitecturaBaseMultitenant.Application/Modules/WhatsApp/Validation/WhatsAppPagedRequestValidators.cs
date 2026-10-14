using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

// Los listados de atención: página, tamaño y búsqueda, sin orden elegible (cada uno tiene el suyo fijo).

internal sealed class WhatsAppNumberListRequestValidator() : PagedRequestValidator<WhatsAppNumberListRequest>([]);

internal sealed class WhatsAppCompanyOptionListRequestValidator() : PagedRequestValidator<WhatsAppCompanyOptionListRequest>([]);

internal sealed class WhatsAppTeamListRequestValidator() : PagedRequestValidator<WhatsAppTeamListRequest>([]);

internal sealed class WhatsAppTeamMemberOptionListRequestValidator() : PagedRequestValidator<WhatsAppTeamMemberOptionListRequest>([]);

internal sealed class WhatsAppConversationMessagesRequestValidator() : PagedRequestValidator<WhatsAppConversationMessagesRequest>([]);

internal sealed class WhatsAppTransferOptionListRequestValidator() : PagedRequestValidator<WhatsAppTransferOptionListRequest>([]);

internal sealed class WhatsAppInboxTeamListRequestValidator() : PagedRequestValidator<WhatsAppInboxTeamListRequest>([]);

internal sealed class WhatsAppContactChildListRequestValidator() : PagedRequestValidator<WhatsAppContactChildListRequest>([]);

internal sealed class WhatsAppTagListRequestValidator() : PagedRequestValidator<WhatsAppTagListRequest>([]);

internal sealed class PersonalWhatsAppConversationListRequestValidator() : PagedRequestValidator<PersonalWhatsAppConversationListRequest>([]);

internal sealed class WhatsAppContactListRequestValidator : PagedRequestValidator<WhatsAppContactListRequest>
{
    public WhatsAppContactListRequestValidator() : base([])
    {
        RuleFor(request => request.Tag).MaximumLength(Domain.WhatsApp.WhatsAppTextLimits.Tag).WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.TooLong"));
        RuleFor(request => request.Consent).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}

internal sealed class WhatsAppConversationListRequestValidator : PagedRequestValidator<WhatsAppConversationListRequest>
{
    public WhatsAppConversationListRequestValidator() : base([])
    {
        RuleFor(request => request.State).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.TeamId).Must(value => value != Guid.Empty)
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}
