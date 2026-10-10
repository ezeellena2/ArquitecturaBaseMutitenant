using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantDeleteRequestValidator : AbstractValidator<AssistantDeleteRequest>
{
    public AssistantDeleteRequestValidator()
    {
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
    }
}
