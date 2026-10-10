using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class UpdateAssistantPolicyRequestValidator : AbstractValidator<UpdateAssistantPolicyRequest>
{
    public UpdateAssistantPolicyRequestValidator()
    {
        RuleFor(request => request.AllowOwnKey).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
        RuleFor(request => request.Reason).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.Reason).MaximumLength(TextLimits.Description).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
    }
}
