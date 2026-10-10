using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantOrganizationPolicyConfiguration : IEntityTypeConfiguration<AssistantOrganizationPolicy>
{
    public void Configure(EntityTypeBuilder<AssistantOrganizationPolicy> builder)
    {
        builder.ToTable("AssistantOrganizationPolicies", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.OrganizationId).IsUnique();
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        builder.OwnsOne(value => value.MonthlyBudget, money =>
        {
            money.Property(value => value.Amount).HasColumnName("MonthlyBudgetAmount").HasPrecision(18, 6);
            money.Property(value => value.Currency).HasColumnName("MonthlyBudgetCurrency").HasMaxLength(CurrencyCode.Length)
                .HasConversion(value => value.Value, value => CurrencyCode.Create(value).Value);
        });
        builder.Navigation(value => value.MonthlyBudget).IsRequired();
    }
}
