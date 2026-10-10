using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Configurations;

internal sealed class AssistantFileCleanupConfiguration : IEntityTypeConfiguration<AssistantFileCleanup>
{
    public void Configure(EntityTypeBuilder<AssistantFileCleanup> builder)
    {
        builder.ToTable("AssistantFileCleanups", Schemas.Tenant);
        builder.HasKey(row => new { row.TenantId, row.Id });
        builder.HasOne<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(row => row.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(row => row.StorageKey).HasMaxLength(TextLimits.WebAddress);
        builder.HasIndex(row => new { row.TenantId, row.CreatedAtUtc, row.Id });
    }
}
