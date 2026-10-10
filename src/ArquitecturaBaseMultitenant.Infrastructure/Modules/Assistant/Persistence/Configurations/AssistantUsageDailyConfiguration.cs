using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantUsageDailyConfiguration : IEntityTypeConfiguration<AssistantUsageDaily>
{
    public void Configure(EntityTypeBuilder<AssistantUsageDaily> builder)
    {
        builder.ToTable("AssistantUsageDaily", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.TenantId, value.UserId, value.Day }).IsUnique();
        builder.OwnsOne(value => value.Cost, money =>
        {
            money.Property(value => value.Amount).HasColumnName("CostAmount").HasPrecision(24, 12);
            money.Property(value => value.Currency).HasColumnName("CostCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.Cost).IsRequired();
    }
}
