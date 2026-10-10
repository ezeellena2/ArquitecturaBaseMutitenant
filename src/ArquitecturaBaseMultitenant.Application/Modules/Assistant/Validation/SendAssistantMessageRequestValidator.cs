using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class SendAssistantMessageRequestValidator : AbstractValidator<SendAssistantMessageRequest>
{
    public SendAssistantMessageRequestValidator()
    {
        RuleFor(request => request.Text).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TextRequired"));
        RuleFor(request => request.Text).MaximumLength(TextLimits.AssistantMessage).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TextTooLong"));
        RuleFor(request => request.AttachmentIds).Must(values => values is not null && values.All(value => value != Guid.Empty) && values.Distinct().Count() == values.Count).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.IdsInvalid"));
        RuleFor(request => request.AttachmentIds).Must(values => values is null || values.Count <= 5).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.IdsInvalid"));
        RuleFor(request => request.ConversationVersion).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
    }
}
