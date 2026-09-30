using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

/// <summary>Mapea el índice global de perfiles de una cuenta. Es una proyección de membresías para elegir acceso sin saltar RLS sobre tenant.Members.</summary>
internal sealed class UserTenantAccessConfiguration : IEntityTypeConfiguration<UserTenantAccess>
{
    public void Configure(EntityTypeBuilder<UserTenantAccess> builder)
    {
        builder.ToTable("UserTenantAccesses", Schemas.Identity);
        builder.HasKey(access => new { access.UserId, access.TenantId });
        builder.Property(access => access.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(access => access.TenantId);
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(access => access.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Domain.Tenancy.Tenant>().WithMany()
            .HasForeignKey(access => access.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
