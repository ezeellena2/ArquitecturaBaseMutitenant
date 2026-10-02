using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

internal sealed class ReservedTenantSlugConfiguration : IEntityTypeConfiguration<ReservedTenantSlug>
{
    public void Configure(EntityTypeBuilder<ReservedTenantSlug> builder)
    {
        builder.ToTable("ReservedTenantSlugs", Schemas.Platform);
        builder.HasKey(item => item.Slug);
        builder.Property(item => item.Slug).HasMaxLength(TenantSlug.MaxLength).IsRequired();
        builder.HasData(ReservedTenantSlug.Defaults);
    }
}
