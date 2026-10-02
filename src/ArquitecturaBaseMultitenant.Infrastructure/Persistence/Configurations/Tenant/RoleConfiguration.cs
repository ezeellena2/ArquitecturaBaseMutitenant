using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Mapea roles privados y versionados de una organización.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", Schemas.Tenant);
        builder.HasKey(role => new { role.TenantId, role.Id });
        builder.Property(role => role.Name).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(role => role.NormalizedName).HasMaxLength(TextLimits.ShortName).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(TextLimits.Description);
        builder.Property(role => role.Scope).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(role => role.Permissions).HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field).HasColumnType("text[]").IsRequired();
        builder.HasIndex(role => new { role.TenantId, role.NormalizedName }).IsUnique();
        builder.HasIndex(role => new { role.TenantId, role.Name, role.Id });
        builder.HasIndex(role => new { role.TenantId, role.CreatedAtUtc, role.Id });
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(role => role.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany()
            .HasForeignKey(role => new { role.TenantId, role.CompanyId }).OnDelete(DeleteBehavior.Restrict);
    }
}
