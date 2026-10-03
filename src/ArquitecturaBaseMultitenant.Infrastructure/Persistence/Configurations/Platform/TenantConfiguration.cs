using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        builder.Property(tenant => tenant.Slug).HasMaxLength(TenantSlug.MaxLength);
        builder.Property(t => t.RegistrationSource).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(t => t.CompanyCount).HasDefaultValue(0).ValueGeneratedOnAddOrUpdate();
        builder.Property(t => t.MemberCount).HasDefaultValue(0).ValueGeneratedOnAddOrUpdate();
        builder.Property(t => t.CompanyCount).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(t => t.CompanyCount).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(t => t.MemberCount).Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.Property(t => t.MemberCount).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
        builder.HasIndex(tenant => new { tenant.Kind, tenant.Status });
        builder.HasIndex(t => new { t.Name, t.Id });
        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id });
    }
}
