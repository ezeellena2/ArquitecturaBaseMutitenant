using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantNoticeRequestValidator : AbstractValidator<AssistantNoticeRequest>
{
    public AssistantNoticeRequestValidator()
    {
        RuleFor(request => request.NoticeVersion).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.NoticeVersion).MaximumLength(TextLimits.Description).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
    }
}
