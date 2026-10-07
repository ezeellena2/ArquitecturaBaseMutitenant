using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Ledger global técnico de retención, sin datos personales copiados ni filtros de tenant.</summary>
internal sealed class ConsumerDataRetentionConfiguration : IEntityTypeConfiguration<ConsumerDataRetention>
{
    public void Configure(EntityTypeBuilder<ConsumerDataRetention> builder)
    {
        builder.ToTable("ConsumerDataRetentions", Schemas.Identity);
        builder.HasKey(retention => retention.Id);
        builder.Property(retention => retention.EntityTypeKey).HasMaxLength(TextLimits.Description).IsRequired();
        builder.Property(retention => retention.Reason).HasMaxLength(TextLimits.Description).IsRequired();
        builder.HasIndex(retention => new { retention.EntityTypeKey, retention.ConsumerTenantId,
            retention.BusinessTenantId, retention.EntityId }).IsUnique().HasDatabaseName("UX_ConsumerDataRetentions_SharedRow");
        builder.HasIndex(retention => new { retention.RetainUntilUtc, retention.Id })
            .HasFilter("\"CopyReplacedAtUtc\" IS NULL").HasDatabaseName("IX_ConsumerDataRetentions_Pending");
        builder.HasOne<Domain.Tenancy.Tenant>().WithMany().HasForeignKey(retention => retention.ConsumerTenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Tenancy.Tenant>().WithMany().HasForeignKey(retention => retention.BusinessTenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
