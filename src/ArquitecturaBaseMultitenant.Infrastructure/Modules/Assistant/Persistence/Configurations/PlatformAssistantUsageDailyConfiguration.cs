using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantUsageDailyConfiguration : IEntityTypeConfiguration<PlatformAssistantUsageDaily>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantUsageDaily> builder)
    {
        builder.ToTable("PlatformAssistantUsageDaily", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => new { value.UserId, value.Day }).IsUnique();
        builder.OwnsOne(value => value.Cost, money =>
        {
            money.Property(value => value.Amount).HasColumnName("CostAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("CostCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.Cost).IsRequired();
    }
}
