using ArquitecturaBaseMultitenant.Domain.Common;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Platform;

/// <summary>Mapea el registro global de espacios personales y organizaciones. Conserva el slug único y los estados que usan las lecturas de acceso.</summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("Tenants", Schemas.Platform);
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Kind).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(tenant => tenant.Name).HasMaxLength(TextLimits.OrganizationName).IsRequired();
        builder.Property(tenant => tenant.Slug).HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
        builder.HasIndex(tenant => new { tenant.Kind, tenant.Status });
    }
}
