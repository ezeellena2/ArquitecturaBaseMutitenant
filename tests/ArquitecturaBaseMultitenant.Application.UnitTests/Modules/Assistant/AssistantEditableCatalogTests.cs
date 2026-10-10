using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Validation;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantEditableCatalogTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("capability")]
    [InlineData("no-efforts")]
    [InlineData("duplicate-efforts")]
    [InlineData("unknown-effort")]
    [InlineData("input")]
    [InlineData("output")]
    [InlineData("output-above-input")]
    [InlineData("negative-price")]
    [InlineData("zero-price")]
    [InlineData("precision")]
    [InlineData("currency")]
    [InlineData("tier-missing-price")]
    [InlineData("tier-zero-threshold")]
    [InlineData("tier-unreachable")]
    public void Incomplete_or_incoherent_metadata_is_rejected_before_writing(string mutation)
    {
        var data = AssistantSettingsTests.Technical();
        var price = data.CacheReadPricePerMillion!;
        var tier = new AssistantModelLongContextRequest(100, price, price, price, price, price);
        data = mutation switch
        {
            "missing" => null,
            "capability" => data with { SupportsAdaptiveThinking = null },
            "no-efforts" => data with { Efforts = [] },
            "duplicate-efforts" => data with { Efforts = [AssistantEffort.Low, AssistantEffort.Low] },
            "unknown-effort" => data with { Efforts = [(AssistantEffort)99] },
            "input" => data with { MaxInputTokens = 0 },
            "output" => data with { MaxOutputTokens = -1 },
            "output-above-input" => data with { MaxOutputTokens = 100001 },
            "negative-price" => data with { CacheReadPricePerMillion = new(-1m, price.Currency) },
            "zero-price" => data with { CacheReadPricePerMillion = new(0m, price.Currency) },
            "precision" => data with { CacheWrite5MinutesPricePerMillion = new(0.1234567m, price.Currency) },
            "currency" => data with { CacheWrite1HourPricePerMillion = new(1m, CurrencyCode.Create("EUR").Value) },
            "tier-missing-price" => data with { LongContext = tier with { CacheReadPricePerMillion = null } },
            "tier-zero-threshold" => data with { LongContext = tier with { InputTokensThreshold = 0 } },
            "tier-unreachable" => data with { LongContext = tier with { InputTokensThreshold = data.MaxInputTokens } },
            _ => throw new InvalidOperationException(),
        };
        var create = new CreateAssistantModelRequest("Test", AssistantProvider.Anthropic, "new-model", price, price, true, data);
        Assert.False(new CreateAssistantModelRequestValidator().Validate(create).IsValid);
        Assert.False(new UpdateAssistantModelRequestValidator().Validate(new UpdateAssistantModelRequest("Test", AssistantProvider.Anthropic, "new-model", price, price, true, 1, data)).IsValid);
    }

    [Fact]
    public void Lower_capacity_and_missing_chat_capabilities_are_saved_but_never_chat_eligible()
    {
        var request = AssistantSettingsTests.Technical();
        var price = request.CacheReadPricePerMillion!;
        var technical = AssistantModelReferenceBuilder.ToResponse(AssistantModelReferenceBuilder.ToDomain(request))!;
        var model = new AssistantModelPriceResponse(Guid.CreateVersion7(), "Test", AssistantProvider.Anthropic, "new-model", price, price, true, 1, technical);
        Assert.True(AssistantModelCompatibilityPolicy.Supports(model, AssistantEffort.High));
        foreach (var incompatible in new[] { technical with { SupportsText = false }, technical with { SupportsTools = false },
            technical with { SupportsAdaptiveThinking = false }, technical with { MaxInputTokens = 51199 }, technical with { MaxOutputTokens = 2047 } })
            Assert.False(AssistantModelCompatibilityPolicy.IsCompatible(model with { Technical = incompatible }));
        Assert.False(AssistantModelCompatibilityPolicy.IsCompatible(model with { Technical = null }));
        Assert.False(AssistantModelCompatibilityPolicy.IsCompatible(model with { IsActive = false }));
        Assert.False(AssistantModelCompatibilityPolicy.Supports(model with { Technical = technical with { Efforts = [AssistantEffort.Low] } }, AssistantEffort.High));
        Assert.True(AssistantModelCompatibilityPolicy.IsCompatible(model with { Technical = technical with { SupportsImages = false, SupportsPdf = false } }));
        Assert.True(new AssistantModelTechnicalRequestValidator().Validate(request with { MaxInputTokens = 100, MaxOutputTokens = 50 }).IsValid);
    }

    [Fact]
    public void Effective_revision_changes_with_prices_or_capabilities_and_does_not_reprice_a_historical_snapshot()
    {
        var request = AssistantSettingsTests.Technical();
        var price = new Money(.123456m, request.CacheReadPricePerMillion!.Currency);
        var technical = AssistantModelReferenceBuilder.ToResponse(AssistantModelReferenceBuilder.ToDomain(request))!;
        var model = new AssistantModelPriceResponse(Guid.CreateVersion7(), "Test", AssistantProvider.Anthropic, "new-model", price, price, true, 1, technical);
        var reference = AssistantModelReferenceBuilder.Build(model);
        var snapshot = AssistantPriceSnapshotBuilder.Build(model, reference);
        var changed = model with { InputPricePerMillion = new(.654321m, price.Currency), Technical = technical with { CacheReadPricePerMillion = new(.123456m, price.Currency) } };
        Assert.NotEqual(reference.Revision, AssistantModelReferenceBuilder.Build(changed).Revision);
        Assert.NotEqual(reference.Revision, AssistantModelReferenceBuilder.Build(model with { Technical = technical with { SupportsPdf = false } }).Revision);
        Assert.Equal(reference.Revision, AssistantModelReferenceBuilder.Build(model with { Name = "Renamed", IsActive = false, Version = 2 }).Revision);
        Assert.Equal(reference.Revision, AssistantModelReferenceBuilder.Build(model with { OutputPricePerMillion = new(.123456000m, price.Currency), Technical = technical with { Efforts = [AssistantEffort.High, AssistantEffort.Medium, AssistantEffort.Low] } }).Revision);
        Assert.Equal(.123456m, snapshot.Input.Amount);
        Assert.Equal(.01m, snapshot.CacheRead.Amount);
        Assert.Equal(reference.Revision, snapshot.Revision);
    }
}
