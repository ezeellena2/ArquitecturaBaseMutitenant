using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantUploadRequestValidator : AbstractValidator<AssistantUploadRequest>
{
    public AssistantUploadRequestValidator()
    {
        RuleFor(request => request.ConversationVersion).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
        RuleFor(request => request.FileName).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.FileInvalid"));
        RuleFor(request => request.ByteLength).Must(value => value is > 0 and <= 10485760).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.FileInvalid"));
        RuleFor(request => request.MediaType).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.FileInvalid"));
    }
}
