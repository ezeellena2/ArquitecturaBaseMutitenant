using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;

internal sealed class AssistantModelTechnicalRequestValidator : AbstractValidator<AssistantModelTechnicalRequest>
{
    public AssistantModelTechnicalRequestValidator()
    {
        RuleFor(value => value.SupportsText).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(value => value.SupportsTools).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(value => value.SupportsImages).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(value => value.SupportsPdf).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(value => value.SupportsAdaptiveThinking).NotNull().WithMessage(_ => AssistantTexts.Get("Assistant.Validation.Required"));
        RuleFor(value => value.Efforts).Must(value => value is { Count: > 0 } && value.All(Enum.IsDefined)
            && value.Distinct().Count() == value.Count).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.EffortsInvalid"));
        RuleFor(value => value.MaxInputTokens).Must(value => value > 0).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalLimitsInvalid"));
        RuleFor(value => value.MaxOutputTokens).Must((model, value) => value > 0 && value <= model.MaxInputTokens)
            .WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalLimitsInvalid"));
        RuleFor(value => value.CacheWrite5MinutesPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
        RuleFor(value => value.CacheWrite1HourPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
        RuleFor(value => value.CacheReadPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
        When(value => value.LongContext is not null, () =>
        {
            RuleFor(value => value.LongContext!.InputTokensThreshold).Must((model, value) => value > 0 && value < model.MaxInputTokens)
                .WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalLimitsInvalid"));
            RuleFor(value => value.LongContext!.InputPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
            RuleFor(value => value.LongContext!.OutputPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
            RuleFor(value => value.LongContext!.CacheWrite5MinutesPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
            RuleFor(value => value.LongContext!.CacheWrite1HourPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
            RuleFor(value => value.LongContext!.CacheReadPricePerMillion).Must(PositiveTariff).WithMessage(_ => AssistantTexts.Get("Assistant.Validation.TechnicalPriceInvalid"));
        });
    }
    private static bool PositiveTariff(Money? value) => AssistantContractPolicy.IsValidTariff(value) && value!.Amount > 0;
}
