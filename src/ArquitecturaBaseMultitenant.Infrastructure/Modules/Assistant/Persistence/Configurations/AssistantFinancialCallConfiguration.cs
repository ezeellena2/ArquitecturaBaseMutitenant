using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantFinancialCallConfiguration : IEntityTypeConfiguration<AssistantFinancialCall>
{
    public void Configure(EntityTypeBuilder<AssistantFinancialCall> builder)
    {
        builder.ToTable("AssistantFinancialCalls", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasOne<AssistantFinancialOperation>().WithMany().HasForeignKey(value => value.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.OperationId, value.Sequence }).IsUnique();
        builder.OwnsOne(value => value.Price, price => AssistantFinancialMapping.Price(price, "Price"));
        builder.Navigation(value => value.Price).IsRequired();
        builder.OwnsOne(value => value.Cost, money => AssistantFinancialMapping.Money(money, "Cost"));
        AssistantFinancialMapping.CurrencyReference(builder, "Cost");
        builder.Navigation(value => value.Cost).IsRequired();
    }
}
