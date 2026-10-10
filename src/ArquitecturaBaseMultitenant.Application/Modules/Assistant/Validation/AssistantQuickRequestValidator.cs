using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantQuickRequestValidator : AbstractValidator<AssistantQuickRequest>
{
    public AssistantQuickRequestValidator()
    {
        RuleFor(request => request).Must(request => request.ConversationId is null && request.Version is null || request.ConversationId is { } id && id != Guid.Empty && request.Version is not null).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.QuickVersionInvalid")).OverridePropertyName("version");
    }
}
