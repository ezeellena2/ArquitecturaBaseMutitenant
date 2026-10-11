using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantDurableOperationConfiguration : IEntityTypeConfiguration<AssistantDurableOperation>
{
    public void Configure(EntityTypeBuilder<AssistantDurableOperation> builder)
    {
        builder.ToTable("AssistantDurableOperations", Schemas.Tenant);
        builder.HasKey(value => new { value.TenantId, value.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(value => value.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(value => value.Access).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.Kind).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.QuickKey).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.SnapshotJson).HasColumnType("jsonb");
        builder.Property(value => value.OriginalSnapshotVersion).HasConversion<long?>().HasColumnType("bigint");
        builder.Property(value => value.SubjectUserIds).HasColumnType("uuid[]");
        builder.HasIndex(value => value.SubjectUserIds).HasMethod("gin");
        builder.HasIndex(value => new { value.TenantId, value.OwnerUserId, value.Access, value.Key }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.ConversationId, value.Id });
        // No conversation FK: deleting content must not remove the key's technical tombstone.
    }
}
