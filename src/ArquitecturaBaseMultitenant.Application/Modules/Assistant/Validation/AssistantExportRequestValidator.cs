using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantExportRequestValidator : AbstractValidator<AssistantExportRequest>
{
    public AssistantExportRequestValidator()
    {
        RuleFor(request => request.Format).Must(value => value is null || Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
    }
}
