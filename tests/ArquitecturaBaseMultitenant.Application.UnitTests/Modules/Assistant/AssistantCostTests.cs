using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantCostTests
{
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);
    internal static AssistantPriceSnapshot Price(decimal input = .1m, decimal output = .5m) =>
        AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "fixture-haiku", "revision-one", Usd(input), Usd(output),
            Usd(.125m), Usd(.2m), Usd(.01m), null, null, null, null, null, null);
    [Fact]
    public void One_hundred_subcent_calls_accumulate_without_rounding_to_cents()
    {

        var result = AssistantCostPolicy.Calculate(Price(), new(1000, 100, 0, 0, 0, 0, 30, true), false);
        Assert.Equal(.00015m, result.Cost.Amount);
        Assert.Equal(.015m, result.Cost.Amount * 100);
    }
    [Fact]
    public void Inclusive_input_and_reasoning_are_not_billed_twice_and_write_TTL_is_distinct()
    {
        var result = AssistantCostPolicy.Calculate(Price(), new(1000, 100, 700, 100, 50, 0, 30, true), false);
        Assert.Equal(.0000945m, result.Cost.Amount);
        Assert.False(result.IsEstimated);
    }
    [Fact]
    public void Missing_counts_and_unknown_write_TTL_preserve_a_conservative_cost()
    {
        var price = Price();
        var unknown = AssistantCostPolicy.Calculate(price, new(null, null, null, null, null, null, null, false), false);
        Assert.Equal(.011264m, unknown.Cost.Amount);
        Assert.True(unknown.IsEstimated);
        var unknownTtl = AssistantCostPolicy.Calculate(price, new(1000, 100, 0, 0, 0, 1000, null, true), false);
        Assert.Equal(.00025m, unknownTtl.Cost.Amount);
        Assert.True(unknownTtl.IsEstimated);
        var missingCache = AssistantCostPolicy.Calculate(price, new(1000, 100, null, null, null, null, null, true), false);
        Assert.Equal(.00025m, missingCache.Cost.Amount);
    }
    [Fact]
    public void Five_total_calls_include_title_and_fit_numeric_24_12_at_the_maximum_permitted_tariff()
    {
        var price = Price();
        Assert.Equal(AssistantCostPolicy.ReserveCall(price, false).Amount * 5,
            AssistantCostPolicy.ReserveTurn(price, Price()).Amount);
        var huge = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "huge", "huge-ref", Usd(999_999_999_999.999999m),
            Usd(999_999_999_999.999999m), Usd(999_999_999_999.999999m), Usd(999_999_999_999.999999m),
            Usd(999_999_999_999.999999m), null, null, null, null, null, null);
        var reservation = AssistantCostPolicy.ReserveTurn(huge, huge);
        Assert.True(reservation.Amount < 1_000_000_000_000m);
        Assert.Equal(266_239_999_999.999999733760m, reservation.Amount);
    }
    [Fact]
    public void Personalized_base_prices_keep_absolute_reference_cache_and_long_tier_and_revision()
    {
        var reference = new AssistantProviderReference("v1", "fixture", "Fixture", "Fixture", Usd(.1m), Usd(.5m),
            Usd(.125m), Usd(.2m), Usd(.01m), 1000, Usd(.2m), Usd(1m), Usd(.25m), Usd(.4m), Usd(.02m), true, true, true, true, [AssistantEffort.Low]);
        var model = new AssistantModelPriceResponse(Guid.CreateVersion7(), "Fixture", AssistantProvider.Anthropic, "fixture", Usd(10m), Usd(20m), true, 1);
        var snapshot = AssistantPriceSnapshotBuilder.Build(model, reference);
        var shortCost = AssistantCostPolicy.Calculate(snapshot, new(1000, 100, 0, 0, 0, 0, null, true), false);
        var longCost = AssistantCostPolicy.Calculate(snapshot, new(1001, 100, 0, 0, 0, 0, null, true), false);
        Assert.Equal(.012m, shortCost.Cost.Amount);
        Assert.Equal(.0003002m, longCost.Cost.Amount);
        Assert.Equal(.125m, snapshot.CacheWrite5Minutes.Amount);
        Assert.Equal("v1", snapshot.Revision);
        Assert.True(AssistantCostPolicy.ReserveCall(snapshot, false).Amount >= shortCost.Cost.Amount);
        Assert.Equal(10m, snapshot.Input.Amount);
    }
    [Fact]
    public void Cumulative_call_updates_replace_and_finalized_usage_cannot_be_replayed_as_new_cost()
    {
        var price = Price();
        var call = AssistantFinancialCall.Create(Guid.CreateVersion7(), 1, false, price, 51200, 2048, AssistantCostPolicy.ReserveCall(price, false));
        call.ReplaceUsage(1000, 50, 0, 0, 0, 0, 10, false, Usd(.000125m));
        call.ReplaceUsage(1000, 100, 0, 0, 0, 0, 30, false, Usd(.00015m));
        Assert.Equal(1000, call.InputTokens);
        Assert.Equal(100, call.OutputTokens);
        Assert.Equal(.00015m, call.Cost.Amount);
        call.FinalizeUsage();
        call.ReplaceUsage(1000, 200, 0, 0, 0, 0, null, false, Usd(.0002m));
        Assert.Equal(.00015m, call.Cost.Amount);
    }
    [Fact]
    public void Remaining_is_only_visible_at_ten_and_daily_reset_is_UTC()
    {
        var day = new DateOnly(2026, 10, 10);
        Assert.Null(AssistantLimitGuard.Describe(39, 0, 50, day).Remaining);
        Assert.Equal(10, AssistantLimitGuard.Describe(40, 0, 50, day).Remaining);
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), AssistantLimitGuard.Describe(40, 0, 50, day).ResetAtUtc);
    }
}
