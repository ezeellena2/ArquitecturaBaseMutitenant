using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantSnapshotRetentionIndexConfiguration : IEntityTypeConfiguration<AssistantSnapshotRetentionIndex>
{
    public void Configure(EntityTypeBuilder<AssistantSnapshotRetentionIndex> builder)
    {
        builder.ToTable("AssistantSnapshotRetentionIndices", Schemas.Platform);
        builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.OperationId).IsUnique();
        builder.HasIndex(value => new { value.ExpiresAtUtc, value.Id });
    }
}
