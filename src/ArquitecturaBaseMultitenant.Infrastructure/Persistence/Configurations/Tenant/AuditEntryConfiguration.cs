using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", Schemas.Tenant);
        builder.HasKey(entry => new { entry.TenantId, entry.Id });
        builder.Property(entry => entry.ActorKind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(entry => entry.Action).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(entry => entry.EntityType).HasMaxLength(TextLimits.OrganizationName).IsRequired();
        builder.Property(entry => entry.Changes).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAtUtc, entry.Id });
    }
}
