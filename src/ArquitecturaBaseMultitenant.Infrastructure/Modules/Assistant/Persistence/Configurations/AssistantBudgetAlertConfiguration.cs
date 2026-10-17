using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantBudgetAlertConfiguration : IEntityTypeConfiguration<AssistantBudgetAlert>
{
    public void Configure(EntityTypeBuilder<AssistantBudgetAlert> builder)
    {
        builder.ToTable("AssistantBudgetAlerts", Schemas.Platform);
        builder.HasKey(row => row.Id);
        builder.HasIndex(row => new { row.MonthId, row.Funding, row.Threshold }).IsUnique();
        builder.Property(row => row.Funding).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.OwnsOne(row => row.Used, money => AssistantFinancialMapping.Money(money, "Used"));
        AssistantFinancialMapping.CurrencyReference(builder, "Used");
        builder.Navigation(row => row.Used).IsRequired();
        builder.OwnsOne(row => row.Budget, money => AssistantFinancialMapping.Money(money, "Budget"));
        AssistantFinancialMapping.CurrencyReference(builder, "Budget");
        builder.Navigation(row => row.Budget).IsRequired();
    }
}
