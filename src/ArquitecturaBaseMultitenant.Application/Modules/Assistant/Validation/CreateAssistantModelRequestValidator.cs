using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using FluentValidation;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class CreateAssistantModelRequestValidator : AbstractValidator<CreateAssistantModelRequest>
{
    public CreateAssistantModelRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.Name).MaximumLength(TextLimits.Description).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
        RuleFor(request => request.Provider).Must(value => value is not null && Enum.IsDefined(value.Value)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.SelectionInvalid"));
        RuleFor(request => request.ProviderModelId).NotEmpty().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(request => request.ProviderModelId).MaximumLength(TextLimits.Description).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.LimitsInvalid"));
        RuleFor(request => request.InputPricePerMillion).Must(value => value is not null && (value.Amount >= 0 && value.Currency.Value == AssistantContractPolicy.BillingCurrency)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PriceInvalid"));
        RuleFor(request => request.OutputPricePerMillion).Must(value => value is not null && (value.Amount >= 0 && value.Currency.Value == AssistantContractPolicy.BillingCurrency)).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.PriceInvalid"));
        RuleFor(request => request.IsActive).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
    }
}
