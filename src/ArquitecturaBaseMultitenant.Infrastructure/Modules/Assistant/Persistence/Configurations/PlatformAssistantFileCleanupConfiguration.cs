using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class PlatformAssistantFileCleanupConfiguration : IEntityTypeConfiguration<PlatformAssistantFileCleanup>
{
    public void Configure(EntityTypeBuilder<PlatformAssistantFileCleanup> builder)
    {
        builder.ToTable("PlatformAssistantFileCleanups", Schemas.Platform);
        builder.HasKey(row => row.Id);
        builder.Property(row => row.StorageKey).HasMaxLength(TextLimits.WebAddress);
        builder.HasIndex(row => new { row.CreatedAtUtc, row.Id });
    }
}
