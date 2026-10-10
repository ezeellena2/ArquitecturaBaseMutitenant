using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantBudgetMonthConfiguration : IEntityTypeConfiguration<AssistantBudgetMonth>
{
    public void Configure(EntityTypeBuilder<AssistantBudgetMonth> builder)
    {
        builder.ToTable("AssistantBudgetMonths", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.TimeZoneId).IsRequired();
        builder.HasOne<ReferenceTimeZone>().WithMany().HasForeignKey(value => value.TimeZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(value => new { value.OrganizationId, value.Month }).IsUnique();
        builder.HasIndex(value => new { value.OrganizationId, value.StartUtc, value.EndUtc });
    }
}
