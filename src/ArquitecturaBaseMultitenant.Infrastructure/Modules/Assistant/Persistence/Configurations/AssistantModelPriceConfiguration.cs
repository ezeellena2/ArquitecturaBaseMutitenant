using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantModelPriceConfiguration : IEntityTypeConfiguration<AssistantModelPrice>
{
    public void Configure(EntityTypeBuilder<AssistantModelPrice> builder)
    {
        builder.ToTable("AssistantModelPrices", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Provider).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.ProviderModelId).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(value => value.DisplayName).HasMaxLength(TextLimits.Description).IsRequired();
        builder.HasIndex(value => new { value.Provider, value.ProviderModelId }).IsUnique().HasDatabaseName(UniqueKeys.AssistantModel);
        builder.OwnsOne(value => value.InputPrice, money =>
        {
            money.Property(value => value.Amount).HasColumnName("InputPriceAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("InputPriceCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.InputPrice).IsRequired();
        builder.OwnsOne(value => value.OutputPrice, money =>
        {
            money.Property(value => value.Amount).HasColumnName("OutputPriceAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("OutputPriceCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.OutputPrice).IsRequired();
        builder.OwnsOne(value => value.Technical, technical =>
        {
            technical.Property(value => value.MaxInputTokens).HasColumnName("TechnicalMaxInputTokens").IsRequired();
            technical.Property(value => value.MaxOutputTokens).HasColumnName("TechnicalMaxOutputTokens").IsRequired();
            foreach (var name in new[] { nameof(AssistantModelTechnical.SupportsText), nameof(AssistantModelTechnical.SupportsTools),
                nameof(AssistantModelTechnical.SupportsImages), nameof(AssistantModelTechnical.SupportsPdf),
                nameof(AssistantModelTechnical.SupportsAdaptiveThinking), nameof(AssistantModelTechnical.LowEffort),
                nameof(AssistantModelTechnical.MediumEffort), nameof(AssistantModelTechnical.HighEffort) })
                technical.Property<bool>(name).HasColumnName("Technical" + name).IsRequired();
            technical.Property(value => value.LongInputTokensThreshold).HasColumnName("TechnicalLongInputTokensThreshold");
            foreach (var name in new[] { nameof(AssistantModelTechnical.CacheWrite5Minutes), nameof(AssistantModelTechnical.CacheWrite1Hour),
                nameof(AssistantModelTechnical.CacheRead), nameof(AssistantModelTechnical.LongInput), nameof(AssistantModelTechnical.LongOutput),
                nameof(AssistantModelTechnical.LongCacheWrite5Minutes), nameof(AssistantModelTechnical.LongCacheWrite1Hour), nameof(AssistantModelTechnical.LongCacheRead) })
            {
                var prefix = "Technical" + name;
                var currency = technical.Property<string>(prefix + "Currency").HasColumnName(prefix + "Currency").HasMaxLength(CurrencyCode.Length)
                    .IsRequired(!name.StartsWith("Long", StringComparison.Ordinal)).Metadata;
                // The owned Money writes this shared column. Its FK mirror must not write a stale value when Money is removed.
                currency.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                currency.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
                technical.HasOne<Domain.ReferenceData.Currency>().WithMany().HasForeignKey(prefix + "Currency").OnDelete(DeleteBehavior.Restrict);
                technical.OwnsOne<Money>(name, money => AssistantFinancialMapping.Money(money, prefix, tariff: true));
            }
            technical.Navigation(value => value.CacheWrite5Minutes).IsRequired();
            technical.Navigation(value => value.CacheWrite1Hour).IsRequired();
            technical.Navigation(value => value.CacheRead).IsRequired();
        });
    }
}
