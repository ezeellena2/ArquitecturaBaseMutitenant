using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Configurations.Identity;

internal sealed class MemberAccessRoleConfiguration : IEntityTypeConfiguration<MemberAccessRole>
{
    public void Configure(EntityTypeBuilder<MemberAccessRole> builder)
    {
        builder.ToTable("MemberAccessRoles", Schemas.Identity);
        builder.HasKey(row => new { row.TenantId, row.MemberId });
        builder.Property(row => row.RoleName).HasMaxLength(TextLimits.ShortName);
        builder.HasOne<Member>().WithMany().HasForeignKey(row => new { row.TenantId, row.MemberId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
