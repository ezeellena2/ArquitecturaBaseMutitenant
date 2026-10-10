using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal static class AssistantFinancialMapping
{
    public static void Money<TOwner>(OwnedNavigationBuilder<TOwner, Money> money, string prefix, bool tariff = false) where TOwner : class
    {
        money.Property(value => value.Amount).HasColumnName(prefix + "Amount").HasPrecision(tariff ? 18 : 24, tariff ? 6 : 12);
        money.Property(value => value.Currency).HasColumnName(prefix + "Currency").HasMaxLength(CurrencyCode.Length)
            .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
    }
    public static void CurrencyReference<TOwner>(EntityTypeBuilder<TOwner> owner, string prefix, bool required = true) where TOwner : class
    {
        // Table sharing propagates the owned value's provider string. The FK keeps the catalog's CLR key type.
        owner.Property<string>(prefix + "Currency").HasColumnName(prefix + "Currency").HasMaxLength(CurrencyCode.Length).IsRequired(required);
        owner.HasOne<Currency>().WithMany().HasForeignKey(prefix + "Currency").OnDelete(DeleteBehavior.Restrict);
    }
    public static void Price<TOwner>(OwnedNavigationBuilder<TOwner, AssistantPriceSnapshot> price, string prefix) where TOwner : class
    {
        price.Property(value => value.ProviderModelId).HasMaxLength(TextLimits.Description).IsRequired();
        price.Property(value => value.Revision).HasMaxLength(TextLimits.Description).IsRequired();
        price.Property(value => value.BillingCurrency).HasMaxLength(CurrencyCode.Length).IsRequired();
        price.HasOne<Currency>().WithMany().HasForeignKey(value => value.BillingCurrency).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { nameof(AssistantPriceSnapshot.Input), nameof(AssistantPriceSnapshot.Output),
            nameof(AssistantPriceSnapshot.CacheWrite5Minutes), nameof(AssistantPriceSnapshot.CacheWrite1Hour), nameof(AssistantPriceSnapshot.CacheRead),
            nameof(AssistantPriceSnapshot.LongInput), nameof(AssistantPriceSnapshot.LongOutput), nameof(AssistantPriceSnapshot.LongCacheWrite5Minutes),
            nameof(AssistantPriceSnapshot.LongCacheWrite1Hour), nameof(AssistantPriceSnapshot.LongCacheRead) })
        {
            var column = prefix + name + "Currency";
            price.Property<string>(column).HasColumnName(column).HasMaxLength(CurrencyCode.Length).IsRequired(!name.StartsWith("Long", StringComparison.Ordinal));
            price.HasOne<Currency>().WithMany().HasForeignKey(column).OnDelete(DeleteBehavior.Restrict);
            price.OwnsOne<Money>(name, money => Money(money, prefix + name, true));
        }
        price.Navigation(value => value.Input).IsRequired();
        price.Navigation(value => value.Output).IsRequired();
        price.Navigation(value => value.CacheWrite5Minutes).IsRequired();
        price.Navigation(value => value.CacheWrite1Hour).IsRequired();
        price.Navigation(value => value.CacheRead).IsRequired();
    }
}
