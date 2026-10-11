using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class PlatformAssistantUsageRequestValidator : AbstractValidator<PlatformAssistantUsageRequest>
{
    public PlatformAssistantUsageRequestValidator()
    {
        RuleFor(request => request.Month).Must(value => value is { Length: 7 } && value[4] == '-' && value.AsSpan(0, 4).ContainsAnyExceptInRange('0', '9') == false && value.AsSpan(5, 2).ContainsAnyExceptInRange('0', '9') == false && int.TryParse(value.AsSpan(0, 4), out var year) && year is >= 1 and <= 9999 && int.TryParse(value.AsSpan(5, 2), out var month) && month is >= 1 and <= 12).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.MonthInvalid"));
        RuleFor(request => request.Page).Must(value => value is null || value is >= 1 and <= PagedRequest.MaxPage).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PageInvalid"));
        RuleFor(request => request.PageSize).Must(value => value is null || PagedRequest.AllowedPageSizes.Contains(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PageInvalid"));
    }
}
