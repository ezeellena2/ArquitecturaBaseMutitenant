using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantFinancialOperationConfiguration : IEntityTypeConfiguration<AssistantFinancialOperation>
{
    public void Configure(EntityTypeBuilder<AssistantFinancialOperation> builder)
    {
        builder.ToTable("AssistantFinancialOperations", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.Property(value => value.Funding).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.Property(value => value.State).HasConversion<string>().HasMaxLength(TextLimits.ShortCode);
        builder.OwnsOne(value => value.ReservedCost, money => AssistantFinancialMapping.Money(money, "ReservedCost"));
        AssistantFinancialMapping.CurrencyReference(builder, "ReservedCost");
        builder.Navigation(value => value.ReservedCost).IsRequired();
        builder.OwnsOne(value => value.MonthlyBudget, money => AssistantFinancialMapping.Money(money, "MonthlyBudget"));
        AssistantFinancialMapping.CurrencyReference(builder, "MonthlyBudget", false);
        builder.OwnsOne(value => value.Price, price => AssistantFinancialMapping.Price(price, "Price"));
        builder.Navigation(value => value.Price).IsRequired();
        builder.OwnsOne(value => value.TitlePrice, price => AssistantFinancialMapping.Price(price, "TitlePrice"));
        builder.HasOne<AssistantBudgetMonth>().WithMany().HasForeignKey(value => value.BudgetMonthId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.UserId, value.DayUtc });
        builder.HasIndex(value => new { value.BudgetMonthId, value.Funding, value.State });
        builder.HasIndex(value => new { value.Access, value.ScopeTenantId, value.CreatedAtUtc, value.Funding });
    }
}
