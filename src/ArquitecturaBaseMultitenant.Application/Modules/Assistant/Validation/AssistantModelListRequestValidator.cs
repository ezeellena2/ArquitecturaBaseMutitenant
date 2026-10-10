using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantModelListRequestValidator : AbstractValidator<AssistantModelListRequest>
{
    public AssistantModelListRequestValidator()
    {
        RuleFor(request => request.Page).Must(value => value is null or >= 1).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PageInvalid"));
        RuleFor(request => request.PageSize).Must(value => value is null || PagedRequest.AllowedPageSizes.Contains(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PageInvalid"));
        RuleFor(request => request.Search).MaximumLength(TextLimits.AuditSearch).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TextTooLong"));
    }
}
