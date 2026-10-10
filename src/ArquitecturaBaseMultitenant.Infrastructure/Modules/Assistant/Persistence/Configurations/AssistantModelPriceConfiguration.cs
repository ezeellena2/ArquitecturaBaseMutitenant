using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantModelPriceConfiguration : IEntityTypeConfiguration<AssistantModelPrice>
{
    public void Configure(EntityTypeBuilder<AssistantModelPrice> builder)
    {
        builder.ToTable("AssistantModelPrices", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Provider).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.ProviderModelId).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.DisplayName).HasMaxLength(TextLimits.ShortName).IsRequired();
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
    }
}
