using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantFeedbackRequestValidator : AbstractValidator<AssistantFeedbackRequest>
{
    public AssistantFeedbackRequestValidator()
    {
        RuleFor(request => request.Rating).Must(value => value is null || Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
    }
}
