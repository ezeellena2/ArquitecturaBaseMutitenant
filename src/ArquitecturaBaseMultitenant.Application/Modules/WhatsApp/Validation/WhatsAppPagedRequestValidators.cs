using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

// Los listados de atención: página, tamaño y búsqueda. Los que muestran una tabla con columnas de un dato ordenan por ellas
// (264); los demás (selectores y listas hijas) tienen su orden fijo.

internal sealed class WhatsAppNumberListRequestValidator() : PagedRequestValidator<WhatsAppNumberListRequest>([]);

internal sealed class WhatsAppCompanyOptionListRequestValidator() : PagedRequestValidator<WhatsAppCompanyOptionListRequest>([]);

internal sealed class WhatsAppTeamListRequestValidator() : PagedRequestValidator<WhatsAppTeamListRequest>(WhatsAppTeamListRequest.SortableFields);

internal sealed class WhatsAppTemplateListRequestValidator : PagedRequestValidator<WhatsAppTemplateListRequest>
{
    public WhatsAppTemplateListRequestValidator() : base(WhatsAppTemplateListRequest.SortableFields)
    {
        RuleFor(request => request.Status).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
        RuleFor(request => request.Group).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}

internal sealed class WhatsAppBroadcastListRequestValidator : PagedRequestValidator<WhatsAppBroadcastListRequest>
{
    public WhatsAppBroadcastListRequestValidator() : base(WhatsAppBroadcastListRequest.SortableFields)
    {
        RuleFor(request => request.Status).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}

internal sealed class WhatsAppBroadcastDetailRequestValidator : PagedRequestValidator<WhatsAppBroadcastDetailRequest>
{
    public WhatsAppBroadcastDetailRequestValidator() : base(WhatsAppBroadcastDetailRequest.SortableFields)
    {
        RuleFor(request => request.Status).Must(value => value is null || Enum.IsDefined(value.Value))
            .WithMessage(_ => WhatsAppTexts.Get("WhatsApp.Validation.SelectionInvalid"));
    }
}

internal sealed class WhatsAppTeamMemberOptionListRequestValidator() : PagedRequestValidator<WhatsAppTeamMemberOptionListRequest>([]);

internal sealed class WhatsAppConversationMessagesRequestValidator() : PagedRequestValidator<WhatsAppConversationMessagesRequest>([]);

internal sealed class WhatsAppTransferOptionListRequestValidator() : PagedRequestValidator<WhatsAppTransferOptionListRequest>([]);

internal sealed class WhatsAppInboxTeamListRequestValidator() : PagedRequestValidator<WhatsAppInboxTeamListRequest>([]);

internal sealed class WhatsAppContactChildListRequestValidator() : PagedRequestValidator<WhatsAppContactChildListRequest>([]);

internal sealed class WhatsAppTagListRequestValidator() : PagedRequestValidator<WhatsAppTagListRequest>([]);

internal sealed class PersonalWhatsAppConversationListRequestValidator() : PagedRequestValidator<PersonalWhatsAppConversationListRequest>([]);

internal sealed class WhatsAppContactListRequestValidator : PagedRequestValidator<WhatsAppContactListRequest>
{
    public WhatsAppContactListRequestValidator() : base(WhatsAppContactListRequest.SortableFields)
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
