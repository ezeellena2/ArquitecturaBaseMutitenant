using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

public static class AssistantModelCompatibilityPolicy
{
    public static bool IsCompatible(AssistantModelPriceResponse model) => model is { IsActive: true, Provider: AssistantProvider.Anthropic,
        Technical: { SupportsText: true, SupportsTools: true, SupportsAdaptiveThinking: true,
            MaxInputTokens: >= AssistantTechnicalLimits.MaxInputTokens, MaxOutputTokens: >= AssistantTechnicalLimits.MaxOutputTokens,
            Efforts.Count: > 0 } };
    public static bool Supports(AssistantModelPriceResponse model, AssistantEffort effort) =>
        IsCompatible(model) && model.Technical!.Efforts.Contains(effort);
}
