using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal static class AssistantCostPolicy
{
    public static AssistantCalculatedUsage Calculate(AssistantPriceSnapshot price, AssistantCallUsage usage, bool title)
    {
        var input = usage.InputTokens ?? AssistantTechnicalLimits.MaxInputTokens;
        var output = usage.OutputTokens ?? (title ? AssistantTechnicalLimits.TitleMaxOutputTokens : AssistantTechnicalLimits.MaxOutputTokens);
        var read = usage.CacheReadTokens ?? 0;
        var write5 = usage.CacheWrite5MinutesTokens ?? 0;
        var write1 = usage.CacheWrite1HourTokens ?? 0;
        var writeUnknown = usage.CacheWriteUnknownTokens ?? 0;
        if (input < 0 || input > AssistantTechnicalLimits.MaxInputTokens || output < 0
            || output > (title ? AssistantTechnicalLimits.TitleMaxOutputTokens : AssistantTechnicalLimits.MaxOutputTokens)
            || read < 0 || write5 < 0 || write1 < 0 || writeUnknown < 0
            || read > input || write5 > input || write1 > input || writeUnknown > input
            || read + write5 + write1 + writeUnknown > input || usage.ReasoningTokens < 0 || usage.ReasoningTokens > output)
            throw new ArgumentException("Usage must describe one bounded provider call.", nameof(usage));
        var longTier = price.LongPromptThreshold is { } threshold && input > threshold;
        var inputPrice = longTier ? price.LongInput! : price.Input;
        var outputPrice = longTier ? price.LongOutput! : price.Output;
        var readPrice = longTier ? price.LongCacheRead! : price.CacheRead;
        var write5Price = longTier ? price.LongCacheWrite5Minutes! : price.CacheWrite5Minutes;
        var write1Price = longTier ? price.LongCacheWrite1Hour! : price.CacheWrite1Hour;
        var breakdownKnown = usage.CacheBreakdownComplete && usage.CacheReadTokens is not null
            && usage.CacheWrite5MinutesTokens is not null && usage.CacheWrite1HourTokens is not null
            && usage.CacheWriteUnknownTokens is not null;
        var worstInput = new[] { inputPrice.Amount, readPrice.Amount, write5Price.Amount, write1Price.Amount }.Max();
        // Unknown cache categories are priced conservatively, never treated as free input.
        if (usage.InputTokens is null) return new(input, output, read, write5, write1, writeUnknown, usage.ReasoningTokens,
            true, ReserveCall(price, title));
        var inputAmount = !breakdownKnown ? input * worstInput
            : (input - read - write5 - write1 - writeUnknown) * inputPrice.Amount + read * readPrice.Amount
                + write5 * write5Price.Amount + write1 * write1Price.Amount + writeUnknown * Math.Max(write5Price.Amount, write1Price.Amount);
        var amount = (inputAmount + output * outputPrice.Amount) / 1_000_000m;
        return new(input, output, read, write5, write1, writeUnknown, usage.ReasoningTokens,
            usage.InputTokens is null || usage.OutputTokens is null || !breakdownKnown || writeUnknown > 0,
            new Money(amount, price.Input.Currency).Round(12));
    }
    public static Money ReserveCall(AssistantPriceSnapshot price, bool title)
    {
        var inputs = new List<decimal> { price.Input.Amount, price.CacheRead.Amount, price.CacheWrite5Minutes.Amount, price.CacheWrite1Hour.Amount };
        var outputs = new List<decimal> { price.Output.Amount };
        if (price.LongPromptThreshold < AssistantTechnicalLimits.MaxInputTokens)
        {
            inputs.AddRange([price.LongInput!.Amount, price.LongCacheRead!.Amount, price.LongCacheWrite5Minutes!.Amount, price.LongCacheWrite1Hour!.Amount]);
            outputs.Add(price.LongOutput!.Amount);
        }
        return new Money((AssistantTechnicalLimits.MaxInputTokens * inputs.Max()
            + (title ? AssistantTechnicalLimits.TitleMaxOutputTokens : AssistantTechnicalLimits.MaxOutputTokens) * outputs.Max())
                / 1_000_000m, price.Input.Currency).Round(12);
    }

    public static Money ReserveTurn(AssistantPriceSnapshot price, AssistantPriceSnapshot? titlePrice)
    {
        // Five total calls: four chat + optional title, or five chat. Never six.
        var main = ReserveCall(price, false);
        var fifth = titlePrice is null ? main : new Money(Math.Max(main.Amount, ReserveCall(titlePrice, true).Amount), main.Currency);
        return new Money(main.Amount * (AssistantTechnicalLimits.MaxCallsPerTurn - 1), main.Currency).Add(fifth).Round(12);
    }
}
