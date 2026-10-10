using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class UpdateOrganizationAssistantSettingsRequestValidator : AbstractValidator<UpdateOrganizationAssistantSettingsRequest>
{
    public UpdateOrganizationAssistantSettingsRequestValidator()
    {
        RuleFor(request => request.Mode).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.ModelId).Must(value => value is not null && value != Guid.Empty).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.Effort).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.DailyMessageLimit).Must(value => value is null or >= 1).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
        RuleFor(request => request.MonthlyBudget).Must(value => value is null || AssistantContractPolicy.IsValidBudget(value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PriceInvalid"));
        RuleFor(request => request.KeyAction).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.ApiKey).MaximumLength(TextLimits.LongText).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.KeyTooLong"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
        RuleFor(request => request.ApiKey).Must((request, key) => request.KeyAction == AssistantKeyAction.Replace ? !string.IsNullOrWhiteSpace(key) : key is null).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.KeyUnexpected"));
    }
}
