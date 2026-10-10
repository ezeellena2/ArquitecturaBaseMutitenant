using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantConversationListRequestValidator : AbstractValidator<AssistantConversationListRequest>
{
    public AssistantConversationListRequestValidator()
    {
        RuleFor(request => request.Cursor).MaximumLength(TextLimits.WebAddress).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.CursorInvalid"));
        RuleFor(request => request.Limit).Must(value => value is null or >= 1 and <= 100).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PageInvalid"));
    }
}
