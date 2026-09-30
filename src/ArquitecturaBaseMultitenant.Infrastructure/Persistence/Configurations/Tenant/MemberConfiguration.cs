using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using TenantEntity = ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Tenant;

/// <summary>Mapea la membresía privada de una cuenta en una organización. La combinación de tenant y usuario es única para evitar perfiles duplicados.</summary>
internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members", Schemas.Tenant);
        builder.HasKey(member => new { member.TenantId, member.Id });
        builder.Property(member => member.Status).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.Property(member => member.RemovalReason).HasConversion<string>().HasMaxLength(TextLimits.ShortName);
        builder.HasIndex(member => new { member.TenantId, member.UserId }).IsUnique();
        builder.HasOne<TenantEntity>().WithMany()
            .HasForeignKey(member => member.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
