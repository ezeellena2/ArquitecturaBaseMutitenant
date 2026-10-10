using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class UpdatePlatformAssistantSettingsRequestValidator : AbstractValidator<UpdatePlatformAssistantSettingsRequest>
{
    public UpdatePlatformAssistantSettingsRequestValidator()
    {
        RuleFor(request => request.DefaultModelId).Must(value => value is not null && value != Guid.Empty).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.AllowedModelIds).Must(values => values is not null && values.All(value => value != Guid.Empty) && values.Distinct().Count() == values.Count).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.IdsInvalid"));
        RuleFor(request => request.Effort).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.DailyMessageLimit).Must(value => value is >= 1).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
        RuleFor(request => request.MonthlyOrganizationBudget).Must(AssistantContractPolicy.IsValidBudget).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PriceInvalid"));
        RuleFor(request => request.PersonalEnabled).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.RetentionDays).Must(value => value is >= 1).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
        RuleFor(request => request.KeyAction).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.ApiKey).MaximumLength(TextLimits.LongText).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.KeyTooLong"));
        RuleFor(request => request.Version).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.VersionRequired"));
        RuleFor(request => request.ApiKey).Must((request, key) => request.KeyAction == AssistantKeyAction.Replace ? !string.IsNullOrWhiteSpace(key) : key is null).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.KeyUnexpected"));
    }
}
