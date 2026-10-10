using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantUsageFactConfiguration : IEntityTypeConfiguration<AssistantUsageFact>
{
    public void Configure(EntityTypeBuilder<AssistantUsageFact> builder)
    {
        builder.ToTable("AssistantUsageFacts", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.Property(value => value.Funding).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.Property(value => value.PriceRevision).HasMaxLength(TextLimits.Description);
        builder.HasIndex(value => new { value.TenantId, value.CallId }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.DayUtc, value.Funding, value.UserId });
        builder.HasIndex(value => new { value.TenantId, value.BudgetMonthId, value.Funding });
        builder.OwnsOne(value => value.Cost, money => AssistantFinancialMapping.Money(money, "Cost"));
        AssistantFinancialMapping.CurrencyReference(builder, "Cost");
        builder.Navigation(value => value.Cost).IsRequired();
    }
}
