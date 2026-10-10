using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantOrganizationSettingsConfiguration : IEntityTypeConfiguration<AssistantOrganizationSettings>
{
    public void Configure(EntityTypeBuilder<AssistantOrganizationSettings> builder)
    {
        builder.ToTable("AssistantOrganizationSettings", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Mode).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.Effort).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(value => value.MaskedKeySuffix).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(value => value.TenantId).IsUnique();
        builder.OwnsOne(value => value.MonthlyBudget, money =>
        {
            money.Property(value => value.Amount).HasColumnName("MonthlyBudgetAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("MonthlyBudgetCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
    }
}
