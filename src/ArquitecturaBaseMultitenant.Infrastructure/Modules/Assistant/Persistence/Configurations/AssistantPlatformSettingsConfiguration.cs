using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantPlatformSettingsConfiguration : IEntityTypeConfiguration<AssistantPlatformSettings>
{
    public void Configure(EntityTypeBuilder<AssistantPlatformSettings> builder)
    {
        builder.ToTable("AssistantPlatformSettings", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Effort).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.MaskedKeySuffix).HasMaxLength(TextLimits.ShortName);
        builder.OwnsOne(value => value.MonthlyOrganizationBudget, money =>
        {
            money.Property(value => value.Amount).HasColumnName("MonthlyOrganizationBudgetAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("MonthlyOrganizationBudgetCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.MonthlyOrganizationBudget).IsRequired();
    }
}
