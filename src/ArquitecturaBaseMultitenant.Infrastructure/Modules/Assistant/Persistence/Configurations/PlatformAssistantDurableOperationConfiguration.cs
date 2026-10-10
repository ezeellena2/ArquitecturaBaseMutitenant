using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantDurableOperationConfiguration : IEntityTypeConfiguration<PlatformAssistantDurableOperation>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantDurableOperation> builder)
    {
        builder.ToTable("PlatformAssistantDurableOperations", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Kind).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.QuickKey).HasMaxLength(TextLimits.ShortName);
        builder.Property(value => value.SnapshotJson).HasColumnType("jsonb");
        builder.HasIndex(value => new { value.OwnerUserId, value.Key }).IsUnique();
        builder.HasIndex(value => new { value.OwnerUserId, value.ConversationId, value.Id });
    }
}
