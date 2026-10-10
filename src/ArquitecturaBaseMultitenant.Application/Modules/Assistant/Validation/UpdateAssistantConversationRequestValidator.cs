using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class UpdateAssistantConversationRequestValidator : AbstractValidator<UpdateAssistantConversationRequest>
{
    public UpdateAssistantConversationRequestValidator()
    {
        RuleFor(request => request.Title).MaximumLength(TextLimits.ShortName).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TitleTooLong"));
        RuleFor(request => request.IsSaved).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
    }
}
